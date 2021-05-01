# Broadcast Server

A server application that supports multiple concurrent connections and broadcasts the data received from one client to all the other clients.

* To support multiple concurrent connections I decided to implement the server with System.Net.Sockets Namespace that provides asynchronous methods such as: BeginAccept , BeginSend and more. Those methods are non blocking and allowing us to handle many clients in parallel.

   * There is another way to support multiple concurrent connections with the "Select" method.

* To handle the errors and exceptions I wrapped the operations that might fail with try blocks and catch blocks.
I also used the "log4net" Apache framework to log the exceptions data handled during the running of the server(especially if there is a SocketException the exception provides us an error code to that caused the exception).
   * The log file path is "BroadcastServer\BroadcastServer\bin\Debug"  where the .exe file is located.