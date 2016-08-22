using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NetService;

namespace SimpleChatClient_Protobuf
{
    public class ChatClient
    {
        public long ID { get; private set; }
        public string Account { get; set; }
        private ProtoBuf.Meta.TypeModel _model = ProtoBuf.Meta.RuntimeTypeModel.Default;
        private TcpService Service { get; set; }
        private MemoryStream _stream = new MemoryStream();
        private Object _sycStream = new Object();
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

        public void SendChat(string msg)
        {
            SimpleChat.CMsgChat chat = new SimpleChat.CMsgChat();
            chat.chatMsg = msg;
            SendObject(SimpleChat.MESSAGE_ID.CMSG_CHAT, chat);
        }

        public void HandleNewClient(long session)
        {
            ID = session;

            // 접속 했으니 hello 보내기
            SimpleChat.CMsgHello hello = new SimpleChat.CMsgHello();
            hello.account = Account;
            hello.passwd = "";
            SendObject(SimpleChat.MESSAGE_ID.CMSG_HELLO, hello);
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
                        case SimpleChat.MESSAGE_ID.SMSG_HELLO:
                            HandleMessage(sessionId, (SimpleChat.SMsgHello)_model.Deserialize(stream, null, typeof(SimpleChat.SMsgHello)));
                            break;
                        case SimpleChat.MESSAGE_ID.SMSG_CHAT:
                            HandleMessage(sessionId, (SimpleChat.SMsgChat)_model.Deserialize(stream, null, typeof(SimpleChat.SMsgChat)));
                            break;
                        case SimpleChat.MESSAGE_ID.SMSG_BYE:
                            HandleMessage(sessionId, (SimpleChat.SMsgBye)_model.Deserialize(stream, null, typeof(SimpleChat.SMsgBye)));
                            break;
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private void HandleMessage(long sessionId, SimpleChat.SMsgHello msg)
        {
            Console.WriteLine("HELLO 결과 - {0}", msg.returnValue.ToString());
        }

        private void HandleMessage(long sessionId, SimpleChat.SMsgChat msg)
        {
            Console.WriteLine("Chat - Sender:{0}, msg:{1}", msg.sender, msg.chatMsg);
        }

        private void HandleMessage(long sessionId, SimpleChat.SMsgBye msg)
        {
            Console.WriteLine("Byte"); ;
        }
    }

    class Program
    {
        public const string IPString = "127.0.0.1";
        public const int Port = 10000;
        public const int ReceiveBuffer = 4 * 1024;
        public const int MaxConnectionCount = 10000;
        public const int Backlog = 100;
        public static TcpService service = null;
        public static ChatClient client = null;

        static void ConnectionCallback(long session, bool success, System.Net.EndPoint address, Object token)
        {
            if (success)
            {
                client.HandleNewClient(session);
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
        }

        static void MesssageCallback(long session, byte[] buffer, int offset, int length)
        {
            client.HandleMessage(session, buffer, offset, length);
        }

        static void Main(string[] args)
        {
            if (args.Length < 1)
                return;

            TcpServiceConfig config = new TcpServiceConfig();
            config.ReceviceBuffer = ReceiveBuffer;
            config.SendBuffer = ReceiveBuffer;
            config.SendCount = 10;
            config.MaxConnectionCount = MaxConnectionCount;
            config.UpdateSessionIntval = 50;
            config.SessionReceiveTimeout = 30 * 1000;
            config.MessageFactoryAssemblyName = "NetService";
            config.MessageFactoryTypeName = "NetService.Message.SimpleBinaryMessageFactory";

            service = new TcpService(config);
            service.ConnectionEvent += new SessionConnectionEvent(ConnectionCallback);
            service.CloseEvent += new SessionCloseEvent(CloseCallback);
            service.ReceiveEvent += new SessionReceiveEvent(ReceiveCallback);
            service.MessageEvent += new SessionMessageEvent(MesssageCallback);


            client = new ChatClient(service);
            client.Account = args[0];

            service.Run();

            service.StartConnect(IPString, Port, 1000, 0, null);

            Console.WriteLine("starting client");

            while(true)
            {
                string msg = Console.ReadLine();
                if( msg == "quit" )
                    break;
                client.SendChat(msg);
            }
            service.Stop();
        }
    }
}
