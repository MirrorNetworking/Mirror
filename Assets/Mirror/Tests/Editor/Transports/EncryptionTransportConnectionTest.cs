using System;
using System.Collections.Generic;
using System.Text;
using Mirror.Transports.Encryption;
using NUnit.Framework;

namespace Mirror.Tests.Transports
{
    public class EncryptionTransportConnectionTest
    {
        struct Data
        {
            public byte[] data;
            public int channel;
        }
        private EncryptedConnection server;
        private EncryptionCredentials serverCreds;
        Queue<Data> serverRecv = new Queue<Data>();
        private Action serverReady;
        private Action<ArraySegment<byte>, int> serverReceive;
        private Func<ArraySegment<byte>, int, bool> shouldServerSend;
        private EncryptedConnection client;
        private EncryptionCredentials clientCreds;
        Queue<Data> clientRecv = new Queue<Data>();
        private Action clientReady;
        private Action<ArraySegment<byte>, int> clientReceive;
        private Func<ArraySegment<byte>, int, bool> shouldClientSend;

        private double _time;
        private double _timestep = 0.05;
        class ErrorException : Exception
        {
            public ErrorException(string msg) : base(msg) {}
        }

        [SetUp]
        public void Setup()
        {
            serverReady = null;
            serverReceive = null;
            shouldServerSend = null;
            clientReady = null;
            clientReceive = null;
            shouldClientSend = null;
            clientRecv.Clear();
            serverRecv.Clear();

            serverCreds = EncryptionCredentials.Generate();
            server = new EncryptedConnection(serverCreds, false,
                (bytes, channel) =>
                {
                    if (shouldServerSend == null || shouldServerSend(bytes, channel))
                        clientRecv.Enqueue(new Data
                        {
                            data = bytes.ToArray(), channel = channel
                        });
                },
                (bytes, channel) =>
                {
                    serverReceive?.Invoke(bytes, channel);
                },
                () => { serverReady?.Invoke(); },
                (error, s) => throw new ErrorException($"{error}: {s}"));

            clientCreds = EncryptionCredentials.Generate();
            client = new EncryptedConnection(clientCreds, true,
                (bytes, channel) =>
                {
                    if (shouldClientSend == null || shouldClientSend(bytes, channel))
                        serverRecv.Enqueue(new Data
                        {
                            data = bytes.ToArray(), channel = channel
                        });
                },
                (bytes, channel) =>
                {
                    clientReceive?.Invoke(bytes, channel);
                },
                () => { clientReady?.Invoke(); },
                (error, s) => throw new ErrorException($"{error}: {s}. t={_time}"));
        }

        private void Pump()
        {
            _time += _timestep;

            while (clientRecv.TryDequeue(out Data data))
            {
                client.OnReceiveRaw(data.data, data.channel);
            }
            if (!client.IsReady)
            {
                client.TickNonReady(_time);
            }

            while (serverRecv.TryDequeue(out Data data))
            {
                server.OnReceiveRaw(data.data, data.channel);
            }
            if (!server.IsReady)
            {
                server.TickNonReady(_time);
            }
        }
        [TearDown]
        public void TearDown()
        {
        }

        [Test]
        public void TestHandshakeSuccess()
        {
            bool isServerReady = false;
            bool isClientReady = false;
            clientReady = () =>
            {
                Assert.False(isClientReady); // only called once
                Assert.True(client.IsReady); // should be set when called
                isClientReady = true;
            };
            serverReady = () =>
            {
                Assert.False(isServerReady); // only called once
                Assert.True(server.IsReady); // should be set when called
                isServerReady = true;
                server.Send(new byte[]
                {
                    1, 2, 3
                }, Channels.Reliable); // need to send to ready the other side
            };

            while (!isServerReady || !isClientReady)
            {
                if (_time > 20)
                {
                    throw new Exception("Timeout.");
                }
                Pump();
            }
        }

        [Test]
        public void TestHandshakeSuccessWithLoss()
        {
            int clientCount = 0;
            shouldClientSend = (data, channel) =>
            {
                if (channel == Channels.Unreliable)
                {
                    clientCount++;
                    // drop 75% of packets
                    return clientCount % 4 == 0;
                }
                return true;
            };
            int serverCount = 0;
            shouldServerSend = (data, channel) =>
            {
                if (channel == Channels.Unreliable)
                {
                    serverCount++;
                    // drop 75% of packets
                    return serverCount % 4 == 0;
                }
                return true;
            };
            TestHandshakeSuccess();
        }

        private bool ArrayContainsSequence(ArraySegment<byte> haystack, ArraySegment<byte> needle)
        {
            if (needle.Count == 0)
            {
                return true;
            }
            int ni = 0;
            for (int hi = 0; hi < haystack.Count; hi++)
            {
                if (haystack.get_Item(hi) == needle.get_Item(ni))
                {
                    ni++;
                    if (ni == needle.Count)
                    {
                        return true;
                    }
                }
                else
                {
                    ni = 0;
                }
            }
            return false;
        }

        [Test]
        public void TestUtil()
        {
            Assert.True(ArrayContainsSequence(new byte[]
            {
                1, 2, 3, 4
            }, new byte[]
            {
            }));
            Assert.True(ArrayContainsSequence(new byte[]
            {
                1, 2, 3, 4
            }, new byte[]
            {
                1, 2, 3, 4
            }));
            Assert.True(ArrayContainsSequence(new byte[]
            {
                1, 2, 3, 4
            }, new byte[]
            {
                2, 3
            }));
            Assert.True(ArrayContainsSequence(new byte[]
            {
                1, 2, 3, 4
            }, new byte[]
            {
                3, 4
            }));
            Assert.False(ArrayContainsSequence(new byte[]
            {
                1, 2, 3, 4
            }, new byte[]
            {
                1, 3
            }));
            Assert.False(ArrayContainsSequence(new byte[]
            {
                1, 2, 3, 4
            }, new byte[]
            {
                3, 4, 5
            }));

        }
        [Test]
        public void TestDataSecurity()
        {
            byte[] serverData = Encoding.UTF8.GetBytes("This is very important secret server data");
            byte[] clientData = Encoding.UTF8.GetBytes("Super secret data from the client is contained within.");
            bool isServerDone = false;
            bool isClientDone = false;
            clientReady = () =>
            {
                client.Send(clientData, Channels.Reliable);
            };
            serverReady = () =>
            {
                server.Send(serverData, Channels.Reliable);
            };

            shouldServerSend = (bytes, i) =>
            {
                if (i == Channels.Reliable)
                {
                    Assert.False(ArrayContainsSequence(bytes, serverData));
                }
                return true;
            };
            shouldClientSend = (bytes, i) =>
            {
                if (i == Channels.Reliable)
                {
                    Assert.False(ArrayContainsSequence(bytes, clientData));
                }
                return true;
            };

            serverReceive = (bytes, channel) =>
            {
                Assert.AreEqual(Channels.Reliable, channel);
                Assert.AreEqual(bytes, new ArraySegment<byte>(clientData));
                Assert.False(isServerDone);
                isServerDone = true;
            };
            clientReceive = (bytes, channel) =>
            {
                Assert.AreEqual(Channels.Reliable, channel);
                Assert.AreEqual(bytes, new ArraySegment<byte>(serverData));
                Assert.False(isClientDone);
                isClientDone = true;
            };

            while (!isServerDone || !isClientDone)
            {
                if (_time > 20)
                {
                    throw new Exception("Timeout.");
                }
                Pump();
            }
        }

        [Test]
        public void TestBadOpCodeErrors()
        {
            Assert.Throws<ErrorException>(() =>
            {
                shouldServerSend = (bytes, i) =>
                {
                    // mess up the opcode (first byte)
                    bytes.Array[bytes.Offset] += 0xAA;
                    return true;
                };
                // setup
                TestHandshakeSuccess();
            });
        }
        [Test]
        public void TestEarlyDataOpCodeErrors()
        {
            Assert.Throws<ErrorException>(() =>
            {
                shouldServerSend = (bytes, i) =>
                {
                    // mess up the opcode (first byte)
                    bytes.Array[bytes.Offset] = 1; // data
                    return true;
                };
                // setup
                TestHandshakeSuccess();
            });
        }

        [Test]
        public void TestUnexpectedAckOpCodeErrors()
        {
            Assert.Throws<ErrorException>(() =>
            {
                shouldServerSend = (bytes, i) =>
                {
                    // mess up the opcode (first byte)
                    bytes.Array[bytes.Offset] = 2; // start, client doesn't expect this
                    return true;
                };
                // setup
                TestHandshakeSuccess();
            });
        }

        [Test]
        public void TestUnexpectedHandshakeOpCodeErrors()
        {
            Assert.Throws<ErrorException>(() =>
            {
                shouldClientSend = (bytes, i) =>
                {
                    // mess up the opcode (first byte)
                    bytes.Array[bytes.Offset] = 3; // ack, server doesn't expect this
                    return true;
                };
                // setup
                TestHandshakeSuccess();
            });
        }
        [Test]
        public void TestUnexpectedFinOpCodeErrors()
        {
            Assert.Throws<ErrorException>(() =>
            {
                shouldServerSend = (bytes, i) =>
                {
                    // mess up the opcode (first byte)
                    bytes.Array[bytes.Offset] = 4; // fin, client doesn't expect this
                    return true;
                };
                // setup
                TestHandshakeSuccess();
        }
    }
}
