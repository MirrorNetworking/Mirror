using NUnit.Framework;
using System;
using System.Collections;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.TestTools;

namespace Telepathy.Tests
{
    [TestFixture]
    [Ignore("Flaky telepathy tests")]
    public class TransportTest
    {
        // just a random port that will hopefully not be taken
        const int Port = 9587;

        Server server;


        // Unity's nunit does not support async tests
        // so we do this boilerplate to run our async methods
        public IEnumerator RunAsync(Func<Task> block)
        {
            var task = Task.Run(block);

            while (!task.IsCompleted) { yield return null; }
            if (task.IsFaulted) { throw task.Exception; }
        }

        [SetUp]
        public void Setup()
        {
            server = new Server();
            server.Start(Port);

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

        [UnityTest]
        public IEnumerator DisconnectImmediateTest()
        {
            return RunAsync(async () =>
            {
                // telepathy will expect and log a ObjectDisposedException
                LogAssert.ignoreFailingMessages = true;

                var client = new Client();
                await client.ConnectAsync("127.0.0.1", Port);

                // I should be able to disconnect right away
                // if connection was pending,  it should just cancel
                client.Disconnect();

                Assert.That(client.Connected, Is.False);
                Assert.That(client.Connecting, Is.False);

                // restore
                LogAssert.ignoreFailingMessages = false;
            });
        }

        [UnityTest]
        public IEnumerator SpamConnectTest()
        {
            return RunAsync(async () =>
            {
                // telepathy will expect and log a ObjectDisposedException
                LogAssert.ignoreFailingMessages = true;

                var client = new Client();
                for (int i = 0; i < 1000; i++)
                {
                    await client.ConnectAsync("127.0.0.1", Port);
                    Assert.That(client.Connecting || client.Connected, Is.True);
                    client.Disconnect();
                    Assert.That(client.Connected, Is.False);
                    Assert.That(client.Connecting, Is.False);
                }

                // restore
                LogAssert.ignoreFailingMessages = false;
            });
        }

        [UnityTest]
        public IEnumerator SpamSendTest()
        {
            return RunAsync(async () =>
            {
                // BeginSend can't be called again after previous one finished. try
                // to trigger that case.
                var client = new Client();
                await client.ConnectAsync("127.0.0.1", Port);

                Assert.That(client.Connected, Is.True);

                byte[] data = new byte[99999];
                for (int i = 0; i < 1000; i++)
                {
                    client.Send(data);
                }

                client.Disconnect();
                Assert.That(client.Connected, Is.False);
                Assert.That(client.Connecting, Is.False);
            });
        }

        [UnityTest]
        public IEnumerator ReconnectTest()
        {
            return RunAsync(async () =>
            {
                var client = new Client();
                await client.ConnectAsync("127.0.0.1", Port);

                // disconnect and lets try again
                client.Disconnect();
                Assert.That(client.Connected, Is.False);
                Assert.That(client.Connecting, Is.False);

                // connecting should flush message queue  right?
                await client.ConnectAsync("127.0.0.1", Port);
                client.Disconnect();
                Assert.That(client.Connected, Is.False);
                Assert.That(client.Connecting, Is.False);
            });
        }

        [UnityTest]
        public IEnumerator ServerTest()
        {
            return RunAsync(async () =>
            {
                Encoding utf8 = Encoding.UTF8;
                var client = new Client();

                await client.ConnectAsync("127.0.0.1", Port);

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
            });
        }

        [UnityTest]
        public IEnumerator ClientTest()
        {
            return RunAsync(async () =>
            {
                Encoding utf8 = Encoding.UTF8;
                var client = new Client();

                await client.ConnectAsync("127.0.0.1", Port);

                // we  should first receive a connected message
                Message serverConnectMsg = NextMessage(server);
                int id = serverConnectMsg.connectionId;

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
            });
        }

        [UnityTest]
        public IEnumerator ClientKickedCleanupTest()
        {
            return RunAsync(async () =>
            {
                var client = new Client();

                await client.ConnectAsync("127.0.0.1", Port);

                // read connected message on server
                Message serverConnectMsg = NextMessage(server);
                int id = serverConnectMsg.connectionId;

                // server kicks the client
                server.Disconnect(id);

                // wait for client disconnected message
                Message clientDisconnectedMsg = NextMessage(client);
                Assert.That(clientDisconnectedMsg.eventType, Is.EqualTo(EventType.Disconnected));

                // was everything cleaned perfectly?
                // if Connecting or Connected is still true then we wouldn't be able
                // to reconnect otherwise
                Assert.That(client.Connecting, Is.False);
                Assert.That(client.Connected, Is.False);
            });
        }

        [UnityTest]
        public IEnumerator GetConnectionInfoTest()
        {
            return RunAsync(async () =>
            {
                // connect a client
                var client = new Client();
                await client.ConnectAsync("127.0.0.1", Port);

                // get server's connect message
                Message serverConnectMsg = NextMessage(server);
                Assert.That(serverConnectMsg.eventType, Is.EqualTo(EventType.Connected));

                // get server's connection info for that client
                string address = server.GetClientAddress(serverConnectMsg.connectionId);
                Assert.That(address == "127.0.0.1" || address == "::ffff:127.0.0.1");

                client.Disconnect();
            });
        }

        // all implementations should be able to handle 'localhost' as IP too
        [UnityTest]
        public IEnumerator ParseLocalHostTest()
        {
            return RunAsync(async () =>
            {
                // connect a client
                var client = new Client();
                await client.ConnectAsync("localhost", Port);

                // get server's connect message
                Message serverConnectMsg = NextMessage(server);
                Assert.That(serverConnectMsg.eventType, Is.EqualTo(EventType.Connected));

                client.Disconnect();
            });
        }

        // IPv4 needs to work
        [UnityTest]
        public IEnumerator ConnectIPv4Test()
        {
            return RunAsync(async () =>
            {
                // connect a client
                var client = new Client();
                await client.ConnectAsync("127.0.0.1", Port);

                // get server's connect message
                Message serverConnectMsg = NextMessage(server);
                Assert.That(serverConnectMsg.eventType, Is.EqualTo(EventType.Connected));

                client.Disconnect();
            });
        }

        // IPv6 needs to work
        [UnityTest]
        public IEnumerator ConnectIPv6Test()
        {
            return RunAsync(async () =>
            {
                // connect a client
                var client = new Client();
                await client.ConnectAsync("::ffff:127.0.0.1", Port);

                // get server's connect message
                Message serverConnectMsg = NextMessage(server);
                Assert.That(serverConnectMsg.eventType, Is.EqualTo(EventType.Connected));

                client.Disconnect();
            });
        }

        [UnityTest]
        public IEnumerator LargeMessageTest()
        {
            return RunAsync(async () =>
            {
                // connect a client
                var client = new Client();
                await client.ConnectAsync("127.0.0.1", Port);

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
            });
        }

        [UnityTest]
        public IEnumerator AllocationAttackTest()
        {
            return RunAsync(async () =>
            {
                // connect a client
                var client = new Client();
                await client.ConnectAsync("127.0.0.1", Port);

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
            });
        }

        [Test]
        public void ServerStartStopTest()
        {
            // create a server that only starts and stops without ever accepting
            // a connection
            var sv = new Server();
            Assert.That(sv.Start(Port + 1), Is.EqualTo(true));
            Assert.That(sv.Active, Is.EqualTo(true));
            sv.Stop();
            Assert.That(sv.Active, Is.EqualTo(false));
        }

        [Test]
        public void ServerStartStopRepeatedTest()
        {
            // can we start/stop on the same port repeatedly?
            var sv = new Server();
            for (int i = 0; i < 10; ++i)
            {
                Assert.That(sv.Start(Port + 1), Is.EqualTo(true));
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
