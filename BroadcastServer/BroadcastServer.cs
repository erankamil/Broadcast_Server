using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.IO;


namespace BroadcastServer
{
    class BroadcastServer
    {
        public const string LocalHost = "127.0.0.1";
        public static int MaxConnetctions = 60;
        public static int MaxBytesRecieve = 64;
        private static Mutex ListMutex = new Mutex();
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        public readonly int ListenPort;
        private ManualResetEvent allDone;
        public Socket listenSocket { get; set; } 
        public List<SocketState> Sockets { get; set; } 


        public BroadcastServer(int listenPort) 
        {
            Sockets = new List<SocketState>();
            allDone = new ManualResetEvent(false);
            this.ListenPort = listenPort;
        }


        private void initializeListener()
        {
            try
            {
                listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                System.Net.IPAddress ipaddress = System.Net.IPAddress.Parse(LocalHost);
                IPEndPoint localEndPoint = new IPEndPoint(ipaddress, ListenPort);
                listenSocket.Bind(localEndPoint);   
                listenSocket.Listen(MaxConnetctions);
            }
            catch (SocketException se)
            {
                string msg = string.Format("{0} Error code:{1}",se.Message, se.ErrorCode);
                log.Error(msg);
                listenSocket?.Close();
                listenSocket?.Dispose();
                throw;
            }
        }

        public void StartListening()
        {
            initializeListener();

            while (true)
            {
                try
                {
                    allDone.Reset();
                    listenSocket.BeginAccept(null, MaxBytesRecieve, new AsyncCallback(acceptConnectionCallback), listenSocket);

                    // Wait until a connection is made and processed before continuing 
                    allDone.WaitOne();
                }
                // in case that error occurred when attempting to access the socket or  Socket object has been closed.
                catch (Exception ex) when (ex is SocketException || ex is ObjectDisposedException)
                {
                    writeErrLog(ex);
                }
                // other cases that we want to terminate  the server:
                // The accepting socket is not listening for connections/ MaxBytesRecieve is < 0
                catch (Exception ex)
                {
                    log.Error(ex.Message);
                    throw;
                }
            }
        }

        private void acceptConnectionCallback(IAsyncResult ar)
        {
            while (true)
            {
                //successfully accepted remote host, can keep accepting new clients
                allDone.Set();
                SocketState socketState = null;
                bool addedToList = false;

                try
                {
                    byte[] Buffer;
                    int bytesTransferred;
                    Socket handler = listenSocket.EndAccept(out Buffer, out bytesTransferred, ar);
                    socketState = new SocketState(handler);
                    if (addedToList = addSocket(socketState))
                    {

                        handler.BeginReceive(socketState.buffer, 0, socketState.MaxBytesRecieve, SocketFlags.None,
                            new AsyncCallback(readCallback), socketState);
                    }
                }
                catch (Exception ex)
                {
                    writeErrLog(ex);
                    if (addedToList)
                    {
                        removeSocket(0, socketState);
                    }
                    else
                    {
                        socketState?.socket?.Close();
                        socketState?.socket?.Dispose();
                    }
                }
            }
        }

        private void readCallback(IAsyncResult ar)
        {
            SocketState socketState = (SocketState)ar.AsyncState;

            try
            {
                Socket workSocket = socketState.socket;
                int bytesRead = workSocket.EndReceive(ar);

                if (bytesRead > 0)
                {
                    //when using onnection-oriented protocol EndReceive will read all message as the size given in beginRecieve
                    log.Debug($"broadcasting with message:{Encoding.ASCII.GetString(socketState.buffer)}");
                    broadcast(workSocket, socketState.buffer);
                }
                else
                {
                    //remote host has closed the connection
                    removeSocket(0, socketState);
                }
            }
            catch (Exception ex)
            {
                writeErrLog(ex);
                if (socketState != null)
                {
                    removeSocket(0, socketState);
                }
            }
        }

        private void broadcast(Socket workSocket, byte[] msg)
        {
            string msgToSend = Encoding.ASCII.GetString(msg);
            msgToSend += "\r\n";
            byte[] buffToSend = Encoding.ASCII.GetBytes(msgToSend);

            try
            {
              ListMutex.WaitOne();

                for(int i = 0 ; i < Sockets.Count; i++)
                {
                    SocketState currSocket = Sockets[i];
                    ListMutex.ReleaseMutex();

                    if (IsSocketConnected(currSocket.socket))
                    {
                        //broadcast all clients except the workSocket
                        if (currSocket.socket != workSocket)
                        {
                            try
                            {
                                currSocket.socket.BeginSend(buffToSend, 0, buffToSend.Length, SocketFlags.None, new AsyncCallback(sendCallBack), currSocket);
                            }
                            catch(SocketException se)
                            {
                                ListMutex.ReleaseMutex();
                                writeErrLog(se);
                                removeSocket(0, currSocket);
                            }
                        }
                    }
                    else
                    {
                        removeSocket(i);
                    }

                    ListMutex.WaitOne();
                }

            ListMutex.ReleaseMutex();

            }
            catch (Exception ex)
            {
                ListMutex.ReleaseMutex();
                writeErrLog(ex);
            }

        }

        private bool IsSocketConnected(Socket s) => !((s.Poll(100, SelectMode.SelectRead) && (s.Available == 0)) || !s.Connected);

        private void sendCallBack(IAsyncResult ar)
        {
            SocketState socketState = null;
            try
            {
                socketState = (SocketState)ar.AsyncState;
                Socket socket = socketState.socket;
                int byteSent = socket.EndSend(ar);
            }
            catch (Exception ex)
            {
                writeErrLog(ex);
                if (socketState != null)
                {
                    removeSocket(0, socketState);
                }
            }
        }
    
        private void writeErrLog(Exception ex)
        {
            string msg;
            if (ex is SocketException)
            {
                msg = string.Format("{0} , Error code:{1}.",
                ex.Message, (ex as SocketException).ErrorCode);
            }
            else
            {
                msg = ex.Message;
            }

            log.Error(msg);
        }

        private bool addSocket(SocketState socketState)
        {
            bool hasAdded = false;
            ListMutex.WaitOne();

            if (Sockets.Count < MaxConnetctions)
            {
                Sockets.Add(socketState);
                hasAdded = true;
            }

            ListMutex.ReleaseMutex();

            if(!hasAdded)
            {
                socketState.socket.Close();
                socketState.socket.Dispose();
            }

            return hasAdded;
        }


        private void removeSocket(int index, SocketState socketState = null)
        {
            ListMutex.WaitOne();

            if (socketState == null)
            {
                Sockets[index].socket.Close();
                Sockets[index].socket.Dispose();
                Sockets.RemoveAt(index);
            }
            else
            {
                socketState.socket.Close();
                socketState.socket.Dispose();
                Sockets.Remove(socketState);
            }

            ListMutex.ReleaseMutex();
        }
    }
}
