using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text;
using System.Net.Sockets;
using System.Net;

namespace BroadcastServer
{
    public class SocketState
    {
        public Socket socket { get; set; }     
        public byte[] buffer { get; set; }
        public static int MaxBytesRecv = 64;
        public int MaxBytesRecieve { get { return MaxBytesRecv; } }

        public SocketState(Socket handler)
        {
            socket = handler;
            buffer = new byte[MaxBytesRecv];
        }
    };
}
