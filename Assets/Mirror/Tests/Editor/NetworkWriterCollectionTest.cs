using System;
using Mirror.Tests.RemoteAttrributeTest;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class NetworkWriterCollectionTest
    {
        [Test]
        public void HasWriteFunctionForInt()
        {
            Assert.That(Writer<int>.write, Is.Not.Null, "int write function was not found");

            Action<NetworkWriter, int> action = NetworkWriterExtensions.WriteInt;
            Assert.That(Writer<int>.write, Is.EqualTo(action), "int write function was incorrect value");
        }

        [Test]
        public void HasReadFunctionForInt()
        {
            Assert.That(Reader<int>.read, Is.Not.Null, "int read function was not found");

            Func<NetworkReader, int> action = NetworkReaderExtensions.ReadInt;
            Assert.That(Reader<int>.read, Is.EqualTo(action), "int read function was incorrect value");
        }

        [Test]
        public void HasWriteNetworkBehaviourFunction()
        {
            Assert.That(Writer<NetworkBehaviour>.write, Is.Not.Null, "NetworkBehaviour read function was not found");

            Action<NetworkWriter, NetworkBehaviour> action = NetworkWriterExtensions.WriteNetworkBehaviour;
            Assert.That(Writer<NetworkBehaviour>.write, Is.EqualTo(action), "NetworkBehaviour read function was incorrect value");
        }

        [Test]
        public void HasReadNetworkBehaviourFunction()
        {
            Assert.That(Reader<NetworkBehaviour>.read, Is.Not.Null, "NetworkBehaviour read function was not found");

            Func<NetworkReader, NetworkBehaviour> actionNonGeneric = NetworkReaderExtensions.ReadNetworkBehaviour;
            Func<NetworkReader, NetworkBehaviour> actionGeneric = NetworkReaderExtensions.ReadNetworkBehaviour<NetworkBehaviour>;
            Assert.That(Reader<NetworkBehaviour>.read, Is.EqualTo(actionNonGeneric).Or.EqualTo(actionGeneric),
                "NetworkBehaviour read function was incorrect value, should be generic or non-generic");
        }

        [Test]
        public void HasWriteNetworkBehaviourDerivedFunction()
        {
            // needs a networkbehaviour that is included in an Message/Rpc/syncvar for this test
            Assert.That(Writer<RpcNetworkIdentityBehaviour>.write, Is.Not.Null, "RpcNetworkIdentityBehaviour read function was not found");

            Action<NetworkWriter, RpcNetworkIdentityBehaviour> action = NetworkWriterExtensions.WriteNetworkBehaviour;
            Assert.That(Writer<RpcNetworkIdentityBehaviour>.write, Is.EqualTo(action), "RpcNetworkIdentityBehaviour read function was incorrect value");
        }

        [Test]
        public void HasReadNetworkBehaviourDerivedFunction()
        {
            Func<NetworkReader, RpcNetworkIdentityBehaviour> reader = Reader<RpcNetworkIdentityBehaviour>.read;
            Assert.That(reader, Is.Not.Null, "RpcNetworkIdentityBehaviour read function was not found");

            Func<NetworkReader, RpcNetworkIdentityBehaviour> action = NetworkReaderExtensions.ReadNetworkBehaviour<RpcNetworkIdentityBehaviour>;
            Assert.That(reader, Is.EqualTo(action), "RpcNetworkIdentityBehaviour read function was incorrect value");
        }
    }
}
