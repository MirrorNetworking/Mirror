using System;
using System.IO;
using NSubstitute;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class NetworkConnectionTest 
    {
        private NetworkConnection connection;
        private byte[] serializedMessage;
        private IConnection mockTransportConnection;

        private SceneMessage data;

        private NotifyPacket lastSent;

        private Action<INetworkConnection, object> delivered;

        private Action<INetworkConnection, object> lost;

        private byte[] lastSerializedPacket;


        [SetUp]
        public void SetUp()
        {
            data = new SceneMessage();
            mockTransportConnection = Substitute.For<IConnection>();

            void ParsePacket(ArraySegment<byte> data)
            {
                var reader = new NetworkReader(data);
                _ = MessagePacker.UnpackId(reader);
                lastSent = reader.ReadNotifyPacket();

                lastSerializedPacket = new byte[data.Count];
                Array.Copy(data.Array, data.Offset, lastSerializedPacket, 0, data.Count);
            }

            mockTransportConnection.SendAsync(
                Arg.Do<ArraySegment<byte>>(ParsePacket), Channel.Unreliable);

            connection = new NetworkConnection(mockTransportConnection);

            serializedMessage = MessagePacker.Pack(new ReadyMessage());
            connection.RegisterHandler<ReadyMessage>(message => { });

            delivered = Substitute.For<Action<INetworkConnection, object>>();
            lost = Substitute.For<Action<INetworkConnection, object>>();

            connection.NotifyDelivered += delivered;
            connection.NotifyLost += lost;

        }

        [Test]
        public void NoHandler()
        {
            int messageId = MessagePacker.GetId<SceneMessage>();
            var reader = new NetworkReader(new byte[] { 1, 2, 3, 4 });
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            {
                connection.InvokeHandler(messageId, reader, 0);
            });

            Assert.That(exception.Message, Does.StartWith("Unexpected message Mirror.SceneMessage received"));
        }

        [Test]
        public void UnknownMessage()
        {
            _ = MessagePacker.GetId<SceneMessage>();
            var reader = new NetworkReader(new byte[] { 1, 2, 3, 4 });
            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            {
                // some random id with no message
                connection.InvokeHandler(1234, reader, 0);
            });

            Assert.That(exception.Message, Does.StartWith("Unexpected message ID 1234 received"));
        }

        #region Notify


        [Test]
        public void SendsNotifyPacket()
        {
            connection.SendNotify(data, 1);

            Assert.That(lastSent, Is.EqualTo(new NotifyPacket
            {
                Sequence = 1,
                ReceiveSequence = 0,
                AckMask = 0
            }));
        }

        [Test]
        public void SendsNotifyPacketWithSequence()
        {
            connection.SendNotify(data, 1);
            Assert.That(lastSent, Is.EqualTo(new NotifyPacket
            {
                Sequence = 1,
                ReceiveSequence = 0,
                AckMask = 0
            }));

            connection.SendNotify(data, 1);
            Assert.That(lastSent, Is.EqualTo(new NotifyPacket
            {
                Sequence = 2,
                ReceiveSequence = 0,
                AckMask = 0
            }));
            connection.SendNotify(data, 1);
            Assert.That(lastSent, Is.EqualTo(new NotifyPacket
            {
                Sequence = 3,
                ReceiveSequence = 0,
                AckMask = 0
            }));
        }

        [Test]
        public void RaisePacketDelivered()
        {
            connection.SendNotify(data, 1);
            connection.SendNotify(data, 3);
            connection.SendNotify(data, 5);

            delivered.DidNotReceiveWithAnyArgs().Invoke(default, default);

            var reply = new NotifyPacket
            {
                Sequence = 1,
                ReceiveSequence = 3,
                AckMask = 0b111
            };

            connection.ReceiveNotify(reply, new NetworkReader(serializedMessage), Channel.Unreliable);

            Received.InOrder(() =>
            {
                delivered.Invoke(connection, 1);
                delivered.Invoke(connection, 3);
                delivered.Invoke(connection, 5);
            });
        }

        [Test]
        public void RaisePacketNotDelivered()
        {
            connection.SendNotify(data, 1);
            connection.SendNotify(data, 3);
            connection.SendNotify(data, 5);

            delivered.DidNotReceiveWithAnyArgs().Invoke(default, default);

            var reply = new NotifyPacket
            {
                Sequence = 1,
                ReceiveSequence = 3,
                AckMask = 0b001
            };

            connection.ReceiveNotify(reply, new NetworkReader(serializedMessage), Channel.Unreliable);

            Received.InOrder(() =>
            {
                lost.Invoke(connection, 1);
                lost.Invoke(connection, 3);
                delivered.Invoke(connection, 5);
            });
        }

        [Test]
        public void DropDuplicates()
        {
            connection.SendNotify(data, 1);

            var reply = new NotifyPacket
            {
                Sequence = 1,
                ReceiveSequence = 1,
                AckMask = 0b001
            };

            connection.ReceiveNotify(reply, new NetworkReader(serializedMessage), Channel.Unreliable);
            connection.ReceiveNotify(reply, new NetworkReader(serializedMessage), Channel.Unreliable);

            delivered.Received(1).Invoke(connection, 1);
        }


        [Test]
        public void LoseOldPackets()
        {
            for (int i = 1; i < 10; i++)
            {
                var packet = new NotifyPacket
                {
                    Sequence = (ushort)i,
                    ReceiveSequence = 100,
                    AckMask = ~0b0ul
                };
                connection.ReceiveNotify(packet, new NetworkReader(serializedMessage), Channel.Unreliable);
            }

            var reply = new NotifyPacket
            {
                Sequence = 100,
                ReceiveSequence = 100,
                AckMask = ~0b0ul
            };
            connection.ReceiveNotify(reply, new NetworkReader(serializedMessage), Channel.Unreliable);

            connection.SendNotify(data, 1);

            Assert.That(lastSent, Is.EqualTo(new NotifyPacket {
                Sequence = 1,
                ReceiveSequence = 100,
                AckMask = 1
            }));
        }

        [Test]
        public void SendAndReceive()
        {
            connection.SendNotify(data, 1);

            Action<SceneMessage> mockHandler = Substitute.For<Action<SceneMessage>>();
            connection.RegisterHandler(mockHandler);

            connection.TransportReceive(new ArraySegment<byte>(lastSerializedPacket), Channel.Unreliable);
            mockHandler.Received().Invoke(new SceneMessage());
        }

        [Test]
        public void NotAcknowledgedYet()
        {
            connection.SendNotify(data, 1);
            connection.SendNotify(data, 3);
            connection.SendNotify(data, 5);

            var reply = new NotifyPacket
            {
                Sequence = 1,
                ReceiveSequence = 2,
                AckMask = 0b011
            };

            connection.ReceiveNotify(reply, new NetworkReader(serializedMessage), Channel.Unreliable);

            delivered.DidNotReceive().Invoke(Arg.Any<INetworkConnection>(), 5);

            reply = new NotifyPacket
            {
                Sequence = 2,
                ReceiveSequence = 3,
                AckMask = 0b111
            };

            connection.ReceiveNotify(reply, new NetworkReader(serializedMessage), Channel.Unreliable);

            delivered.Received().Invoke(connection, 5);
        }

        #endregion
    }
}