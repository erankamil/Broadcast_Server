using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BroadcastServer
{
    class Program
    {
        static void Main(string[] args)
        {
            log4net.Config.XmlConfigurator.Configure();
            BroadcastServer server = new BroadcastServer(9999);
            try
            {
                server.StartListening();
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }
    }
}
//C:\Users\erank\source\repos\BroadcastServer\broadcastserver.log
//