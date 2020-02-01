using NUnit.Framework;
using System;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine.TestTools;

namespace Telepathy.Tests
{
    [TestFixture]
    public class TransportTest
    {
        // just a random port that will hopefully not be taken
        const int port = 9587;

        Server server;

        [SetUp]
        public void Setup()
        {
            server = new Server();
            server.Start(port);

        }

        [TearDown]
        public void TearDown()
        {
            server.Stop();
        }

        [Test]
        public void NextConnectionIdTest()
        {
            // it should always start at '1', because '0' is reserved for
            // Mirror's local player
            int id = server.NextConnectionId();
            Assert.That(id, Is.EqualTo(1));
        }

        [Test]
        public void DisconnectImmediateTest()
        {
            // telepathy will expect and log a ObjectDisposedException
            LogAssert.ignoreFailingMessages = true;

            Client client = new Client();
            client.Connect("127.0.0.1", port);

            // I should be able to disconnect right away
            // if connection was pending,  it should just cancel
            client.Disconnect();

            Assert.That(client.Connected, Is.False);
            Assert.That(client.Connecting, Is.False);

            // restore
            LogAssert.ignoreFailingMessages = false;
        }

        /* this works fine outside of Unity. but we might have to live with it
           failing inside of Unity sometimes
        [Test]
        public void SpamConnectTest()
        {
            // telepathy will expect and log a ObjectDisposedException
            LogAssert.ignoreFailingMessages = true;

            Client client = new Client();
            for (int i = 0; i < 1000; i++)
            {
                client.Connect("127.0.0.1", port);
                Assert.That(client.Connecting || client.Connected, Is.True);
                client.Disconnect();
                Assert.That(client.Connected, Is.False);
                Assert.That(client.Connecting, Is.False);
            }

            // restore
            LogAssert.ignoreFailingMessages = false;
        }
        */

        [Test]
        public void SpamSendTest()
        {
            // BeginSend can't be called again after previous one finished. try
            // to trigger that case.
            Client client = new Client();
            client.Connect("127.0.0.1", port);

            // wait for successful connection
            Message connectMsg = NextMessage(client);
            Assert.That(connectMsg.eventType, Is.EqualTo(EventType.Connected));
            Assert.That(client.Connected, Is.True);

            byte[] data = new byte[99999];
            for (int i = 0; i < 1000; i++)
            {
                client.Send(data);
            }

            client.Disconnect();
            Assert.That(client.Connected, Is.False);
            Assert.That(client.Connecting, Is.False);
        }


        /* this works fine outside of Unity. but we might have to live with it
           failing inside of Unity sometimes
        [Test]
        public void ReconnectTest()
        {
            Client client = new Client();
            client.Connect("127.0.0.1", port);

            // wait for successful connection
            Message connectMsg = NextMessage(client);
            Assert.That(connectMsg.eventType, Is.EqualTo(EventType.Connected));
            // disconnect and lets try again
            client.Disconnect();
            Assert.That(client.Connected, Is.False);
            Assert.That(client.Connecting, Is.False);

            // connecting should flush message queue  right?
            client.Connect("127.0.0.1", port);
            // wait for successful connection
            connectMsg = NextMessage(client);
            Assert.That(connectMsg.eventType, Is.EqualTo(EventType.Connected));
            client.Disconnect();
            Assert.That(client.Connected, Is.False);
            Assert.That(client.Connecting, Is.False);
        }
        */

        [Test]
        public void ServerTest()
        {
            Encoding utf8 = Encoding.UTF8;
            Client client = new Client();

            client.Connect("127.0.0.1", port);

            // we should first receive a connected message
            Message connectMsg = NextMessage(server);
            Assert.That(connectMsg.eventType, Is.EqualTo(EventType.Connected));

            // then we should receive the data
            client.Send(utf8.GetBytes("Hello world"));
            Message dataMsg = NextMessage(server);
            Assert.That(dataMsg.eventType, Is.EqualTo(EventType.Data));
            string str = utf8.GetString(dataMsg.data);
            Assert.That(str, Is.EqualTo("Hello world"));

            // finally when the client disconnect,  we should get a disconnected message
            client.Disconnect();
            Message disconnectMsg = NextMessage(server);
            Assert.That(disconnectMsg.eventType, Is.EqualTo(EventType.Disconnected));
        }

        [Test]
        public void ClientTest()
        {
            Encoding utf8 = Encoding.UTF8;
            Client client = new Client();

            client.Connect("127.0.0.1", port);

            // we  should first receive a connected message
            Message serverConnectMsg = NextMessage(server);
            int id = serverConnectMsg.connectionId;

            // we  should first receive a connected message
            Message clientConnectMsg = NextMessage(client);
            Assert.That(serverConnectMsg.eventType, Is.EqualTo(EventType.Connected));

            // Send some data to the client
            server.Send(id, utf8.GetBytes("Hello world"));
            Message dataMsg = NextMessage(client);
            Assert.That(dataMsg.eventType, Is.EqualTo(EventType.Data));
            string str = utf8.GetString(dataMsg.data);
            Assert.That(str, Is.EqualTo("Hello world"));

            // finally if the server stops,  the clients should get a disconnect error
            server.Stop();
            Message disconnectMsg = NextMessage(client);
            Assert.That(disconnectMsg.eventType, Is.EqualTo(EventType.Disconnected));

            client.Disconnect();
        }

        [Test]
        public void ServerDisconnectClientTest()
        {
            Client client = new Client();

            client.Connect("127.0.0.1", port);

            // we  should first receive a connected message
            Message serverConnectMsg = NextMessage(server);
            int id = serverConnectMsg.connectionId;

            bool result = server.Disconnect(id);
            Assert.That(result, Is.True);
        }

        [Test]
        public void ClientKickedCleanupTest()
        {
            Client client = new Client();

            client.Connect("127.0.0.1", port);

            // read connected message on client
            Message clientConnectedMsg = NextMessage(client);
            Assert.That(clientConnectedMsg.eventType, Is.EqualTo(EventType.Connected));

            // read connected message on server
            Message serverConnectMsg = NextMessage(server);
            int id = serverConnectMsg.connectionId;

            // server kicks the client
            bool result = server.Disconnect(id);
            Assert.That(result, Is.True);

            // wait for client disconnected message
            Message clientDisconnectedMsg = NextMessage(client);
            Assert.That(clientDisconnectedMsg.eventType, Is.EqualTo(EventType.Disconnected));

            // was everything cleaned perfectly?
            // if Connecting or Connected is still true then we wouldn't be able
            // to reconnect otherwise
            Assert.That(client.Connecting, Is.False);
            Assert.That(client.Connected, Is.False);
        }

        [Test]
        public void GetConnectionInfoTest()
        {
            // connect a client
            Client client = new Client();
            client.Connect("127.0.0.1", port);

            // get server's connect message
            Message serverConnectMsg = NextMessage(server);
            Assert.That(serverConnectMsg.eventType, Is.EqualTo(EventType.Connected));

            // get server's connection info for that client
            string address = server.GetClientAddress(serverConnectMsg.connectionId);
            Assert.That(address == "127.0.0.1" || address == "::ffff:127.0.0.1");

            client.Disconnect();
        }

        // all implementations should be able to handle 'localhost' as IP too
        [Test]
        public void ParseLocalHostTest()
        {
            // connect a client
            Client client = new Client();
            client.Connect("localhost", port);

            // get server's connect message
            Message serverConnectMsg = NextMessage(server);
            Assert.That(serverConnectMsg.eventType, Is.EqualTo(EventType.Connected));

            client.Disconnect();
        }

        // IPv4 needs to work
        [Test]
        public void ConnectIPv4Test()
        {
            // connect a client
            Client client = new Client();
            client.Connect("127.0.0.1", port);

            // get server's connect message
            Message serverConnectMsg = NextMessage(server);
            Assert.That(serverConnectMsg.eventType, Is.EqualTo(EventType.Connected));

            client.Disconnect();
        }

        // IPv6 needs to work
        [Test]
        public void ConnectIPv6Test()
        {
            // connect a client
            Client client = new Client();
            client.Connect("::ffff:127.0.0.1", port);

            // get server's connect message
            Message serverConnectMsg = NextMessage(server);
            Assert.That(serverConnectMsg.eventType, Is.EqualTo(EventType.Connected));

            client.Disconnect();
        }

        [Test]
        public void LargeMessageTest()
        {
            // connect a client
            Client client = new Client();
            client.Connect("127.0.0.1", port);

            // we should first receive a connected message
            Message serverConnectMsg = NextMessage(server);
            int id = serverConnectMsg.connectionId;

            // Send largest allowed message
            bool sent = client.Send(new byte[server.MaxMessageSize]);
            Assert.That(sent, Is.EqualTo(true));
            Message dataMsg = NextMessage(server);
            Assert.That(dataMsg.eventType, Is.EqualTo(EventType.Data));
            Assert.That(dataMsg.data.Length, Is.EqualTo(server.MaxMessageSize));

            // finally if the server stops,  the clients should get a disconnect error
            server.Stop();
            client.Disconnect();
        }

        [Test]
        public void AllocationAttackTest()
        {
            // connect a client
            Client client = new Client();
            client.Connect("127.0.0.1", port);

            // we should first receive a connected message
            Message serverConnectMsg = NextMessage(server);
            int id = serverConnectMsg.connectionId;

            // allow client to send large message
            int attackSize = server.MaxMessageSize * 2;
            client.MaxMessageSize = attackSize;

            // Send a large message, bigger thank max message size
            // -> this should disconnect the client
            bool sent = client.Send(new byte[attackSize]);
            Assert.That(sent, Is.EqualTo(true));
            Message dataMsg = NextMessage(server);
            Assert.That(dataMsg.eventType, Is.EqualTo(EventType.Disconnected));

            // finally if the server stops,  the clients should get a disconnect error
            server.Stop();
            client.Disconnect();
        }

        [Test]
        public void ServerStartStopTest()
        {
            // create a server that only starts and stops without ever accepting
            // a connection
            Server sv = new Server();
            Assert.That(sv.Start(port + 1), Is.EqualTo(true));
            Assert.That(sv.Active, Is.EqualTo(true));
            sv.Stop();
            Assert.That(sv.Active, Is.EqualTo(false));
        }

        [Test]
        public void ServerStartStopRepeatedTest()
        {
            // can we start/stop on the same port repeatedly?
            Server sv = new Server();
            for (int i = 0; i < 10; ++i)
            {
                Assert.That(sv.Start(port + 1), Is.EqualTo(true));
                Assert.That(sv.Active, Is.EqualTo(true));
                sv.Stop();
                Assert.That(sv.Active, Is.EqualTo(false));
            }
        }

        [Test]
        public void IntToBytesBigTest()
        {
            int number = 0x01020304;

            byte[] numberBytes = Utils.IntToBytesBigEndian(number);
            Assert.That(numberBytes[0], Is.EqualTo(0x01));
            Assert.That(numberBytes[1], Is.EqualTo(0x02));
            Assert.That(numberBytes[2], Is.EqualTo(0x03));
            Assert.That(numberBytes[3], Is.EqualTo(0x04));

            int converted = Utils.BytesToIntBigEndian(numberBytes);
            Assert.That(converted, Is.EqualTo(number));
        }

        [Test]
        public void IntToBytesBigNonAllocTest()
        {
            int number = 0x01020304;

            byte[] numberBytes = new byte[4];
            Utils.IntToBytesBigEndianNonAlloc(number, numberBytes);
            Assert.That(numberBytes[0], Is.EqualTo(0x01));
            Assert.That(numberBytes[1], Is.EqualTo(0x02));
            Assert.That(numberBytes[2], Is.EqualTo(0x03));
            Assert.That(numberBytes[3], Is.EqualTo(0x04));

            int converted = Utils.BytesToIntBigEndian(numberBytes);
            Assert.That(converted, Is.EqualTo(number));
        }

        static Message NextMessage(Server server)
        {
            Message message;
            int count = 0;

            while (!server.GetNextMessage(out message))
            {
                count++;
                Thread.Sleep(100);

                if (count >= 100)
                {
                    Assert.Fail("The message did not get to the server");
                }
            }

            return message;
        }

        static Message NextMessage(Client client)
        {
            Message message;
            int count = 0;

            while (!client.GetNextMessage(out message))
            {
                count++;
                Thread.Sleep(100);

                if (count >= 100)
                {
                    Assert.Fail("The message did not get to the client");
                }
            }

            return message;
        }

    }
}
