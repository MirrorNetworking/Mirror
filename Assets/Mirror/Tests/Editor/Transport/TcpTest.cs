using Mirror.Tcp;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;
using static Mirror.Tests.AsyncUtil;

namespace Mirror.Tests
{
    [TestFixture]
    public class TcpTest
    {
        // just a random port that will hopefully not be taken
        const int Port = 9587;

        Server server;
        Client client;

        Queue<byte[]> clientData;

        SemaphoreSlim clientSemaphore;

        Queue<(int, byte[])> serverData;
        SemaphoreSlim serverSemaphore;
        SemaphoreSlim serverConnectSemaphore;
        SemaphoreSlim serverDisconnectSemaphore;
        SemaphoreSlim clientDisconnectSemaphore;


        #region Async Utilities

        private async Task<(int, byte[])> GetServerData()
        {
            if (await serverSemaphore.WaitAsync(2000))
                return serverData.Dequeue();
            else
                throw new SocketException((int)SocketError.TimedOut);
        }

        private async Task<byte[]> GetClientData(Client client)
        {
            if (await clientSemaphore.WaitAsync(2000))
                return clientData.Dequeue();
            else
                throw new SocketException((int)SocketError.TimedOut);
        }

        private async Task<int> WaitForServerConnect()
        {
            if (await serverConnectSemaphore.WaitAsync(2000))
            {
                (int id, byte[] data) = serverData.Dequeue();

                Assert.That(data, Is.Null);

                return id;
            }
            else
                throw new SocketException((int)SocketError.TimedOut);
        }

        private async Task<int> WaitForServerDisconnect()
        {
            if (await serverDisconnectSemaphore.WaitAsync(2000))
            {
                (int id, byte[] data) = serverData.Dequeue();

                Assert.That(data, Is.Null);

                return id;
            }
            else
                throw new SocketException((int)SocketError.TimedOut);
        }

        private async Task WaitForClientDisconnect()
        {
            if (await clientDisconnectSemaphore.WaitAsync(2000))
                return;
            else
                throw new SocketException((int)SocketError.TimedOut);
        }

        public static async Task<T> TimeoutAfter<T>(Task<T> task, int millisecondsTimeout)
        {
            if (task == await Task.WhenAny(task, Task.Delay(millisecondsTimeout)))
                return await task;
            else
                throw new TimeoutException();
        }

        #endregion

        [SetUp]
        public void Setup()
        {
            server = new Server();
            client = new Client();

            clientData = new Queue<byte[]>();
            clientSemaphore = new SemaphoreSlim(0);

            serverData = new Queue<(int, byte[])>();
            serverSemaphore = new SemaphoreSlim(0);
            serverConnectSemaphore = new SemaphoreSlim(0);
            serverDisconnectSemaphore = new SemaphoreSlim(0);
            clientDisconnectSemaphore = new SemaphoreSlim(0);

            client.ReceivedData += data =>
            {
                Debug.Log("Received client data");
                clientData.Enqueue(data);
                clientSemaphore.Release();
            };
            client.Disconnected += () =>
            {
                clientDisconnectSemaphore.Release();
            };

            server.ReceivedData += (id, data) =>
            {
                serverData.Enqueue((id, data));
                serverSemaphore.Release();
            };

            server.Connected += (id) =>
            {
                serverData.Enqueue((id, null));
                serverConnectSemaphore.Release();
            };
            server.Disconnected += (id) =>
            {
                serverData.Enqueue((id, null));
                serverDisconnectSemaphore.Release();
            };

            server.ReceivedError += (id, ex) =>
            {
                Debug.LogException(ex);
            };

            Debug.Log("Start Listening");
            _ = server.ListenAsync(Port);

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
                
                await client.ConnectAsync("127.0.0.1", Port);

                // I should be able to disconnect right away
                // if connection was pending,  it should just cancel
                client.Disconnect();

                await WaitForServerDisconnect();

                Assert.That(client.Connected, Is.False);
                Assert.That(client.Connecting, Is.False);
            });
        }

        [UnityTest]
        public IEnumerator SpamConnectTest()
        {
            return RunAsync(async () =>
            {
                for (int i = 0; i < 10; i++)
                {
                    await client.ConnectAsync("127.0.0.1", Port);
                    Assert.That(client.Connected, Is.True);
                    client.Disconnect();

                    await WaitForClientDisconnect();
                    await WaitForServerDisconnect();

                    Assert.That(client.Connected, Is.False);
                    Assert.That(client.Connecting, Is.False);
                }
            });
        }

        [UnityTest]
        public IEnumerator SpamSendTest()
        {
            return RunAsync(async () =>
            {
                // BeginSend can't be called again after previous one finished. try
                // to trigger that case.
                await client.ConnectAsync("127.0.0.1", Port);

                Assert.That(client.Connected, Is.True);

                var data = new ArraySegment<byte>(new byte[99999]);
                for (int i = 0; i < 1000; i++)
                {
                    await client.SendAsync(data);
                }

                client.Disconnect();
                await WaitForServerDisconnect();

                Assert.That(client.Connected, Is.False);
                Assert.That(client.Connecting, Is.False);
            });
        }

        [UnityTest]
        public IEnumerator ReconnectTest()
        {
            return RunAsync(async () =>
            {
                await client.ConnectAsync("127.0.0.1", Port);

                // disconnect and lets try again
                client.Disconnect();

                await WaitForServerDisconnect();

                Assert.That(client.Connected, Is.False);
                Assert.That(client.Connecting, Is.False);

                await WaitForClientDisconnect();

                // connecting should flush message queue  right?
                await client.ConnectAsync("127.0.0.1", Port);
                client.Disconnect();

                await WaitForServerDisconnect();
                await WaitForClientDisconnect();

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

                await client.ConnectAsync("127.0.0.1", Port);

                // we should first receive a connected me
                int id = await WaitForServerConnect();

                var  data = new ArraySegment<byte>(utf8.GetBytes("Hello world"));
                // then we should receive the data
                await client.SendAsync(data);

                var (rConnectionId,received) = await GetServerData();

                string str = utf8.GetString(received);
                Assert.That(str, Is.EqualTo("Hello world"));

                // finally when the client disconnect,  we should get a disconnected message
                client.Disconnect();

                var disconnectId = await WaitForServerDisconnect();
                Assert.That(disconnectId, Is.EqualTo(id));
            });
        }


        [UnityTest]
        public IEnumerator ClientTest()
        {
            return RunAsync(async () =>
            {
                Encoding utf8 = Encoding.UTF8;

                Debug.Log("Going to connect");
                await client.ConnectAsync("127.0.0.1", Port);

                Debug.Log("Connected");

                // we  should first receive a connected message
                int id = await WaitForServerConnect();

                Debug.Log($"Got server conncted {id}");

                // Send some data to the client
                server.Send(id, new ArraySegment<byte>(utf8.GetBytes("Hello world")));
                server.Flush();

                byte[] data = await GetClientData(client);

                Debug.Log("Got client data");

                string str = utf8.GetString(data);
                Assert.That(str, Is.EqualTo("Hello world"));

                // finally if the server stops,  the clients should get a disconnect error
                server.Stop();

                await WaitForClientDisconnect();

                client.Disconnect();
                await WaitForServerDisconnect();
            });
        }

        [UnityTest]
        public IEnumerator ClientKickedCleanupTest()
        {
            return RunAsync(async () =>
            {
                await client.ConnectAsync("127.0.0.1", Port);

                int id = await WaitForServerConnect();

                // server kicks the client
                server.Disconnect(id);

                await WaitForClientDisconnect();

                await WaitForServerDisconnect();
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
                await client.ConnectAsync("127.0.0.1", Port);

                int id = await WaitForServerConnect();

                // get server's connection info for that client
                string address = server.GetClientAddress(id);
                Debug.Log($"address {address}");
                Assert.That(address == "127.0.0.1" || address == "::ffff:127.0.0.1");

                client.Disconnect();

                await WaitForServerDisconnect();

            });
        }

        // all implementations should be able to handle 'localhost' as IP too
        [UnityTest]
        public IEnumerator ParseLocalHostTest()
        {
            return RunAsync(async () =>
            {
                // connect a client
                await client.ConnectAsync("localhost", Port);

                // get server's connect message
                int id = await WaitForServerConnect();

                client.Disconnect();

                await WaitForServerDisconnect();
            });
        }

        // IPv4 needs to work
        [UnityTest]
        public IEnumerator ConnectIPv4Test()
        {
            return RunAsync(async () =>
            {
                await client.ConnectAsync("127.0.0.1", Port);

                int id = await WaitForServerConnect();

                client.Disconnect();

                await WaitForServerDisconnect();
            });
        }

        // IPv6 needs to work
        [UnityTest]
        public IEnumerator ConnectIPv6Test()
        {
            return RunAsync(async () =>
            {
                await client.ConnectAsync("::ffff:127.0.0.1", Port);

                int id = await WaitForServerConnect();

                client.Disconnect();

                await WaitForServerDisconnect();
            });
        }

        [UnityTest]
        public IEnumerator LargeMessageTest()
        {
            return RunAsync(async () =>
            {
                await client.ConnectAsync("127.0.0.1", Port);

                int id = await WaitForServerConnect();

                var data = new byte[100000];

                // Send largest allowed message
                await client.SendAsync(new ArraySegment<byte>(data));

                (int rConnectionId, byte[] received) = await GetServerData();

                Assert.That(received.Length, Is.EqualTo(data.Length));

                // finally if the server stops,  the clients should get a disconnect error
                server.Stop();
                client.Disconnect();
            });
        }

        [UnityTest]
        public IEnumerator ServerStartStopRepeatedTest()
        {
            return RunAsync(async () =>
            {
                // can we start/stop on the same port repeatedly?
                var sv = new Server();
                for (int i = 0; i < 10; ++i)
                {
                    Task task = sv.ListenAsync(Port + 1);
                    Assert.That(sv.Active, Is.True);
                    sv.Stop();
                    Assert.That(sv.Active, Is.False);
                    await task;
                }
            });
        }

    }
}
