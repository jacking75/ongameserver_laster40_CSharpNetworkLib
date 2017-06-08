using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NetService;

namespace SimpleChatServer_Protobuf
{
    public class ChatClient
    {
        public long ID { get; set; }
        public string Account { get; set; }

        private MemoryStream _stream = new MemoryStream();
        private Object _sycStream = new Object();
        private ProtoBuf.Meta.TypeModel _model = ProtoBuf.Meta.RuntimeTypeModel.Default;
        private TcpService Service { get; set; }
        public ChatClient(TcpService service)
        {
            this.Service = service;
        }

        public void SendObject(SimpleChat.MESSAGE_ID id, Object obj)
        {
            lock (_sycStream)
            {
                _stream.Seek(0, SeekOrigin.Begin);

                ProtoBuf.Serializer.SerializeWithLengthPrefix<int>(_stream, (int)id, ProtoBuf.PrefixStyle.Fixed32);
                _model.Serialize(_stream, obj);
                Service.SendToSession(ID, _stream.GetBuffer(), 0, (int)_stream.Position, false);
            }
        }
    }

    public class ChatServer
    {
        private ConcurrentDictionary<long, ChatClient> _clients = new ConcurrentDictionary<long, ChatClient>();
        private ProtoBuf.Meta.TypeModel _model = ProtoBuf.Meta.RuntimeTypeModel.Default;
        private TcpService Service { get; set; }
        public ChatServer(TcpService service)
        {
            this.Service = service;
        }
        void SendMessage(long sessionId, SimpleChat.MESSAGE_ID id, Object obj)
        {
            ChatClient client = null;
            _clients.TryGetValue(sessionId, out client);
            
            if (client != null)
            {
                client.SendObject(id, obj);
            }
        }

        public void HandleNewClient(long session)
        {
            ChatClient client = new ChatClient(Service);
            client.ID = session;
            _clients.TryAdd(session, client);
        }

        public void HandleRemoveClient(long session)
        {
            ChatClient client = null;
            _clients.TryRemove(session, out client);
        }

        public void HandleMessage(long sessionId, byte[] buffer, int offset, int length)
        {
            using (MemoryStream stream = new MemoryStream(buffer, offset, length))
            {
                try
                {
                    SimpleChat.MESSAGE_ID id = (SimpleChat.MESSAGE_ID)ProtoBuf.Serializer.DeserializeWithLengthPrefix<int>(stream, ProtoBuf.PrefixStyle.Fixed32);
                    switch (id)
                    {
                        case SimpleChat.MESSAGE_ID.CMSG_HELLO:
                            HandleMessage(sessionId, (SimpleChat.CMsgHello)_model.Deserialize(stream, null, typeof(SimpleChat.CMsgHello)));
                            break;
                        case SimpleChat.MESSAGE_ID.CMSG_CHAT:
                            HandleMessage(sessionId, (SimpleChat.CMsgChat)_model.Deserialize(stream, null, typeof(SimpleChat.CMsgChat)));
                            break;
                        case SimpleChat.MESSAGE_ID.CMSG_BYE:
                            HandleMessage(sessionId, (SimpleChat.CMsgBye)_model.Deserialize(stream, null, typeof(SimpleChat.CMsgBye)));
                            break;
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private void HandleMessage(long sessionId, SimpleChat.CMsgHello req)
        {
            ChatClient client = null;
            _clients.TryGetValue(sessionId, out client);
            SimpleChat.SMsgHello ack = new SimpleChat.SMsgHello();
            if( client!= null)
            {
                client.Account = req.account;
                ack.returnValue = SimpleChat.SMsgHello.RET.OK;
            }
            else
                ack.returnValue = SimpleChat.SMsgHello.RET.FAILED;

            SendMessage(sessionId, SimpleChat.MESSAGE_ID.SMSG_HELLO, ack);
        }

        private void HandleMessage(long sessionId, SimpleChat.CMsgChat req)
        {
            ChatClient client = null;
            _clients.TryGetValue(sessionId, out client);
            SimpleChat.SMsgChat ack = new SimpleChat.SMsgChat();

            if (client != null)
            {
                ack.sender = client.Account;
                ack.chatMsg = req.chatMsg;

                Parallel.ForEach(_clients, (KeyValuePair<long, ChatClient> s) => s.Value.SendObject(SimpleChat.MESSAGE_ID.SMSG_CHAT, ack));
            }
        }

        private void HandleMessage(long sessionId, SimpleChat.CMsgBye req)
        {
            SimpleChat.SMsgBye ack = new SimpleChat.SMsgBye();
            SendMessage(sessionId, SimpleChat.MESSAGE_ID.SMSG_BYE, ack);
        }
    }

    class Program
    {
        public const string IPString = "";
        public const int Port = 10000;
        public const int ReceiveBuffer = 4 * 1024;
        public const int MaxConnectionCount = 10000;
        public const int Backlog = 100;
        public static TcpService service = null;
        public static ChatServer server = null;

        static void ConnectionCallback(long session, bool success, System.Net.EndPoint address, Object token)
        {
            if (success)
            {
                Console.WriteLine("[{0}]접속 완료 - id:{1}, remote:{2}", Thread.CurrentThread.GetHashCode(), session, address);
                server.HandleNewClient(session);
            }
            else
            {
                Console.WriteLine("[{0}]접속 실패 - id:{1}, remote:{2}", Thread.CurrentThread.GetHashCode(), session, address);
            }
        }

        static void CloseCallback(long session, CloseReason reason)
        {
            server.HandleRemoveClient(session);
            Console.WriteLine("[{0}]접속 종료 - remote:{1},id:{2}", Thread.CurrentThread.GetHashCode(), session, reason.ToString());
        }

        static void ReceiveCallback(long session, byte[] buffer, int offset, int length)
        {
        }

        static void MesssageCallback(long session, byte[] buffer, int offset, int length)
        {
            server.HandleMessage(session, buffer, offset, length);
        }

       
        static void Main(string[] args)
        {
            TcpServiceConfig config = new TcpServiceConfig();
            config.ReceviceBuffer = ReceiveBuffer;
            config.SendBuffer = ReceiveBuffer;
            config.SendCount = 10;
            config.MaxConnectionCount = MaxConnectionCount;
            config.UpdateSessionIntval = 50;
            config.SessionReceiveTimeout = 0;// 30 * 1000;

            config.MessageFactoryAssemblyName = "NetService";
            config.MessageFactoryTypeName = "NetService.Message.SimpleBinaryMessageFactory";

            service = new TcpService(config);

            service.ConnectionEvent += new SessionConnectionEvent(ConnectionCallback);
            service.CloseEvent += new SessionCloseEvent(CloseCallback);
            service.ReceiveEvent += new SessionReceiveEvent(ReceiveCallback);
            service.MessageEvent += new SessionMessageEvent(MesssageCallback);

            server = new ChatServer(service);
                        
            service.Run();
            service.StartListener(IPString, Port, Backlog);

            Console.WriteLine("starting server!");

            int update = Environment.TickCount;
            while (true)
            {
                System.Threading.Thread.Sleep(50);
                ConsoleKeyInfo key = Console.ReadKey(true);
                if (key.KeyChar == 'q')
                {
                    break;
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
