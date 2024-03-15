using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mirror.Tests.NetworkServers;
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
        private Func<PubKeyInfo, bool> serverValidateKey;

        private EncryptedConnection client;
        private EncryptionCredentials clientCreds;
        Queue<Data> clientRecv = new Queue<Data>();
        private Action clientReady;
        private Action<ArraySegment<byte>, int> clientReceive;
        private Func<ArraySegment<byte>, int, bool> shouldClientSend;
        private Func<PubKeyInfo, bool> clientValidateKey;

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
            serverValidateKey = null;
            clientReady = null;
            clientReceive = null;
            shouldClientSend = null;
            clientValidateKey = null;
            clientRecv.Clear();
            serverRecv.Clear();
            _time = 0;

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
                (error, s) => throw new ErrorException($"{error}: {s}"),
                info =>
                {
                    if (serverValidateKey != null) return serverValidateKey(info);
                    return true;
                });

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
                (error, s) => throw new ErrorException($"{error}: {s}. t={_time}"),
                info =>
                {
                    if (clientValidateKey != null) return clientValidateKey(info);
                    return true;
                });
        }

        private void Pump()
        {
            _time += _timestep;

            while (clientRecv.TryDequeue(out Data data))
            {
                client.OnReceiveRaw(new ArraySegment<byte>(data.data), data.channel);
            }
            if (!client.IsReady)
            {
                client.TickNonReady(_time);
            }

            while (serverRecv.TryDequeue(out Data data))
            {
                server.OnReceiveRaw(new ArraySegment<byte>(data.data), data.channel);
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
                server.Send(new ArraySegment<byte>(new byte[]
                {
                    1, 2, 3
                }), Channels.Reliable); // need to send to ready the other side
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
                if (haystack.Array[haystack.Offset + hi] == needle.Array[needle.Offset + ni])
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
            Assert.True(ArrayContainsSequence(new ArraySegment<byte>(new byte[]
            {
                1, 2, 3, 4
            }), new ArraySegment<byte>(new byte[]
            {
            })));
            Assert.True(ArrayContainsSequence(new ArraySegment<byte>(new byte[]
            {
                1, 2, 3, 4
            }), new ArraySegment<byte>(new byte[]
            {
                1, 2, 3, 4
            })));
            Assert.True(ArrayContainsSequence(new ArraySegment<byte>(new byte[]
            {
                1, 2, 3, 4
            }), new ArraySegment<byte>(new byte[]
            {
                2, 3
            })));
            Assert.True(ArrayContainsSequence(new ArraySegment<byte>(new byte[]
            {
                1, 2, 3, 4
            }), new ArraySegment<byte>(new byte[]
            {
                3, 4
            })));
            Assert.False(ArrayContainsSequence(new ArraySegment<byte>(new byte[]
            {
                1, 2, 3, 4
            }), new ArraySegment<byte>(new byte[]
            {
                1, 3
            })));
            Assert.False(ArrayContainsSequence(new ArraySegment<byte>(new byte[]
            {
                1, 2, 3, 4
            }), new ArraySegment<byte>(new byte[]
            {
                3, 4, 5
            })));

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
                client.Send(new ArraySegment<byte>(clientData), Channels.Reliable);
            };
            serverReady = () =>
            {
                server.Send(new ArraySegment<byte>(serverData), Channels.Reliable);
            };

            shouldServerSend = (bytes, i) =>
            {
                if (i == Channels.Reliable)
                {
                    Assert.False(ArrayContainsSequence(bytes, new ArraySegment<byte>(serverData)));
                }
                return true;
            };
            shouldClientSend = (bytes, i) =>
            {
                if (i == Channels.Reliable)
                {
                    Assert.False(ArrayContainsSequence(bytes, new ArraySegment<byte>(clientData)));
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
            });
        }
        [Test]
        public void TestBadDataErrors()
        {
            TestHandshakeSuccess();
            Assert.Throws<ErrorException>(() =>
            {
                // setup
                shouldServerSend = (bytes, i) =>
                {
                    // mess up a byte in the data
                    bytes.Array[bytes.Offset + 3] += 1;
                    return true;
                };
                server.Send(new ArraySegment<byte>(new byte[]
                {
                    1, 2, 3, 4
                }), Channels.Reliable);
                Pump();
            });
        }

        [Test]
        public void TestBadPubKeyInStartErrors()
        {
            shouldClientSend = (bytes, i) =>
            {
                if (bytes.Array[bytes.Offset] == 2 /* HandshakeStart Opcode */)
                {
                    // mess up a byte in the data
                    bytes.Array[bytes.Offset + 3] += 1;
                }
                return true;
            };
            Assert.Throws<ErrorException>(() =>
            {
                TestHandshakeSuccess();
            });
        }

        [Test]
        public void TestBadPubKeyInAckErrors()
        {
            shouldServerSend = (bytes, i) =>
            {
                if (bytes.Array[bytes.Offset] == 3 /* HandshakeAck Opcode */)
                {
                    // mess up a byte in the data
                    bytes.Array[bytes.Offset + 3] += 1;
                }
                return true;
            };
            Assert.Throws<ErrorException>(() =>
            {
                TestHandshakeSuccess();
            });
        }

        [Test]
        public void TestDataSizes()
        {
            List<int> sizes = new List<int>();
            sizes.Add(1);
            sizes.Add(2);
            sizes.Add(3);
            sizes.Add(6);
            sizes.Add(9);
            sizes.Add(16);
            sizes.Add(60);
            sizes.Add(100);
            sizes.Add(200);
            sizes.Add(400);
            sizes.Add(800);
            sizes.Add(1024);
            sizes.Add(1025);
            sizes.Add(4096);
            sizes.Add(1024 * 16);
            sizes.Add(1024 * 64);
            sizes.Add(1024 * 128);
            sizes.Add(1024 * 512);
            // removed for performance, these do pass though
            //sizes.Add(1024 * 1024);
            //sizes.Add(1024 * 1024 * 16);
            //sizes.Add(1024 * 1024 * 64); // 64MiB

            TestHandshakeSuccess();
            var maxSize = sizes.Max();
            var sendByte = new byte[maxSize];
            for (uint i = 0; i < sendByte.Length; i++)
            {
                sendByte[i] = (byte)i;
            }
            int size = -1;
            clientReceive = (bytes, channel) =>
            {
                // Assert.AreEqual is super slow for larger arrays, so do it manually
                Assert.AreEqual(bytes.Count, size);
                for (int i = 0; i < size; i++)
                {
                    if (bytes.Array[bytes.Offset + i] != sendByte[i])
                    {
                        Assert.Fail($"received bytes[{i}] did not match. expected {sendByte[i]}, got {bytes.Array[bytes.Offset + i]}");
                    }
                }
            };
            foreach (var s in sizes)
            {
                size = s;
                server.Send(new ArraySegment<byte>(sendByte, 0, size), 1);
                Pump();
            }
        }


        [Test]
        public void TestPubKeyValidationIsCalled()
        {
            bool clientCalled = false;
            clientValidateKey = info =>
            {
                Assert.AreEqual(new ArraySegment<byte>(serverCreds.PublicKeySerialized), info.Serialized);
                Assert.AreEqual(serverCreds.PublicKeyFingerprint, info.Fingerprint);
                clientCalled = true;
                return true;
            };
            bool serverCalled = false;
            serverValidateKey = info =>
            {
                Assert.AreEqual(clientCreds.PublicKeyFingerprint, info.Fingerprint);
                serverCalled = true;
                return true;
            };
            TestHandshakeSuccess();
            Assert.IsTrue(clientCalled);
            Assert.IsTrue(serverCalled);
        }

        [Test]
        public void TestClientPubKeyValidationErrors()
        {
            clientValidateKey = info => false;
            Assert.Throws<ErrorException>(() =>
            {
                TestHandshakeSuccess();
            });
        }

        [Test]
        public void TestServerPubKeyValidationErrors()
        {
            serverValidateKey = info => false;
            Assert.Throws<ErrorException>(() =>
            {
                TestHandshakeSuccess();
            });
        }
    }
}
