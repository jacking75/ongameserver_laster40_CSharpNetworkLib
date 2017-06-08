using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NetService;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace SimpleClient
{
    class Program
    {
        public const string IPString = "127.0.0.1";
        public const int Port = 10000;
        public const int ReceiveBuffer = 4 * 1024;
        public const int MaxConnectionCount = 10000;
        public const int Backlog = 100;
        public static TcpService service = null;

        public const int ClientConnectionCount = 100;

        static public ThreadLocal<Random> rand = null;

        static ConcurrentDictionary<long, long> _sessions = new ConcurrentDictionary<long, long>();

        static void ConnectionCallback(long session, bool success, System.Net.EndPoint address, Object token)
        {
            if (success)
            {
                _sessions.TryAdd(session, session);
                Console.WriteLine("[{0}]접속 완료 - id:{1}, remote:{2}", Thread.CurrentThread.GetHashCode(), session, address);
            }
            else
            {
                Console.WriteLine("[{0}]접속 실패 - id:{1}, remote:{2}", Thread.CurrentThread.GetHashCode(), session, address);
            }
        }

        static void CloseCallback(long session, CloseReason reason)
        {
            Console.WriteLine("[{0}]접속 종료 - remote:{1},id:{2}", Thread.CurrentThread.GetHashCode(), session, reason.ToString());
        }

        static void ReceiveCallback(long session, byte[] buffer, int offset, int length)
        {
            // service.SendToSession(session, buffer, offset, length, false);
        }

        static void Main(string[] args)
        {
            rand = new ThreadLocal<Random>(() => { return new Random(Environment.TickCount); });

            //TcpServiceConfig config = new TcpServiceConfig();
            //config.ReceviceBuffer = ReceiveBuffer;
            //config.SendBuffer = ReceiveBuffer;
            //config.SendCount = 10;
            //config.MaxConnectionCount = MaxConnectionCount;
            //config.UpdateSessionIntval = 50;
            //config.SessionReceiveTimeout = 30 * 1000;
            //config.MessageFactoryAssemblyName = "NetService";
            //config.MessageFactoryTypeName = "NetService.Message.SimpleBinaryMessageFactory";

            service = new TcpService("SimpleClient.xml");
            service.ConnectionEvent += new SessionConnectionEvent(ConnectionCallback);
            service.CloseEvent += new SessionCloseEvent(CloseCallback);
            service.ReceiveEvent += new SessionReceiveEvent(ReceiveCallback);
            service.Run();
            Console.WriteLine("starting server!");
            int update = Environment.TickCount;
            while (true)
            {
                System.Threading.Thread.Sleep(50);

                Parallel.ForEach(_sessions, (KeyValuePair<long, long> s) =>
                {
                    byte[] buf = new byte[1000];
                    for (int i = 0; i < buf.Length; ++i)
                        buf[i] = (byte)i;
                    service.SendToSession(s.Key, buf, 0, buf.Length, false);
                });
                
                if (Console.KeyAvailable)
                {
                    ConsoleKeyInfo key = Console.ReadKey(true);
                    if (key.KeyChar == '1')
                    {
                        service.Stop();
                    }
                    else if (key.KeyChar == '2')
                    {
                        service.Run();
                    }
                    else if (key.KeyChar == '3')
                    {
                    }
                    else if (key.KeyChar == '4')
                    {
                        service.StartConnect(IPString, Port, 1000, 1000, null);

                    }
                    else if (key.KeyChar == 'q')
                    {
                        break;
                    }
                }

                if (Environment.TickCount - update > 5000)
                {
                    Console.WriteLine(service.ToString());
                    update = Environment.TickCount;
                }
            }

            service.Stop();
        }
    }
}
