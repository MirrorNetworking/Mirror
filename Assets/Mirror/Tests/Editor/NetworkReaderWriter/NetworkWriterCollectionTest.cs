using System;
using Mirror.Tests.Rpcs;
using NUnit.Framework;

namespace Mirror.Tests.NetworkReaderWriter
{
    public class NetworkWriterCollectionTest
    {
        // we defined WriteInt and WriteVarInt. make sure it's using the priority one.
        [Test]
        public void UsesPriorityWriteFunctionForInt()
        {
            Assert.That(Writer<int>.write, Is.Not.Null, "int write function was not found");

            Action<NetworkWriter, int> action = NetworkWriterExtensions.WriteVarInt;
            Assert.That(Writer<int>.write, Is.EqualTo(action), "int write function was incorrect value");
        }

        // we defined ReadInt and ReadVarInt. make sure it's using the priority one.
        [Test]
        public void UsesPriorityReadFunctionForInt()
        {
            Assert.That(Reader<int>.read, Is.Not.Null, "int read function was not found");

            Func<NetworkReader, int> action = NetworkReaderExtensions.ReadVarInt;
            Assert.That(Reader<int>.read, Is.EqualTo(action), "int read function was incorrect value");
        }

        // we defined WriteInt and WriteVarUInt. make sure it's using the priority one.
        [Test]
        public void UsesPriorityWriteFunctionForUInt()
        {
            Assert.That(Writer<uint>.write, Is.Not.Null, "uint write function was not found");

            Action<NetworkWriter, uint> action = NetworkWriterExtensions.WriteVarUInt;
            Assert.That(Writer<uint>.write, Is.EqualTo(action), "uint write function was incorrect value");
        }

        // we defined ReadInt and ReadVarInt. make sure it's using the priority one.
        [Test]
        public void UsesPriorityReadFunctionForUInt()
        {
            Assert.That(Reader<uint>.read, Is.Not.Null, "uint read function was not found");

            Func<NetworkReader, uint> action = NetworkReaderExtensions.ReadVarUInt;
            Assert.That(Reader<uint>.read, Is.EqualTo(action), "uint read function was incorrect value");
        }
        // we defined WriteInt and WriteVarInt. make sure it's using the priority one.
        [Test]
        public void UsesPriorityWriteFunctionForLong()
        {
            Assert.That(Writer<long>.write, Is.Not.Null, "long write function was not found");

            Action<NetworkWriter, long> action = NetworkWriterExtensions.WriteVarLong;
            Assert.That(Writer<long>.write, Is.EqualTo(action), "long write function was incorrect value");
        }

        // we defined ReadLong and ReadVarLong. make sure it's using the priority one.
        [Test]
        public void UsesPriorityReadFunctionForLong()
        {
            Assert.That(Reader<long>.read, Is.Not.Null, "long read function was not found");

            Func<NetworkReader, long> action = NetworkReaderExtensions.ReadVarLong;
            Assert.That(Reader<long>.read, Is.EqualTo(action), "long read function was incorrect value");
        }

        // we defined WriteLong and WriteVarULong. make sure it's using the priority one.
        [Test]
        public void UsesPriorityWriteFunctionForULong()
        {
            Assert.That(Writer<ulong>.write, Is.Not.Null, "ulong write function was not found");

            Action<NetworkWriter, ulong> action = NetworkWriterExtensions.WriteVarULong;
            Assert.That(Writer<ulong>.write, Is.EqualTo(action), "ulong write function was incorrect value");
        }

        // we defined ReadLong and ReadVarLong. make sure it's using the priority one.
        [Test]
        public void UsesPriorityReadFunctionForULong()
        {
            Assert.That(Reader<ulong>.read, Is.Not.Null, "ulong read function was not found");

            Func<NetworkReader, ulong> action = NetworkReaderExtensions.ReadVarULong;
            Assert.That(Reader<ulong>.read, Is.EqualTo(action), "ulong read function was incorrect value");
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
