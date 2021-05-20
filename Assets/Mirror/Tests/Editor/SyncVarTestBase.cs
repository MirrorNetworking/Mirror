using System;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class SyncVarTestBase : MirrorTest
    {
        [TearDown]
        public override void TearDown()
        {
            NetworkIdentity.spawned.Clear();
            base.TearDown();
        }

        // TODO remove
        protected NetworkIdentity CreateNetworkIdentity(uint netId)
        {
            CreateNetworked(out GameObject gameObject, out NetworkIdentity networkIdentity);
            networkIdentity.netId = netId;
            NetworkIdentity.spawned[netId] = networkIdentity;
            return networkIdentity;
        }

        /// <returns>If data was written by OnSerialize</returns>
        public static bool SyncToClient<T>(T serverObject, T clientObject, bool initialState) where T : NetworkBehaviour
        {
            bool written = ServerWrite(serverObject, initialState, out ArraySegment<byte> data, out int writeLength);
            ClientRead(clientObject, initialState, data, writeLength);
            return written;
        }

        public static bool ServerWrite<T>(T serverObject, bool initialState, out ArraySegment<byte> data, out int writeLength) where T : NetworkBehaviour
        {
            NetworkWriter writer = new NetworkWriter();
            bool written = serverObject.OnSerialize(writer, initialState);
            writeLength = writer.Position;
            data = writer.ToArraySegment();
            return written;
        }

        public static void ClientRead<T>(T clientObject, bool initialState, ArraySegment<byte> data, int writeLength) where T : NetworkBehaviour
        {
            NetworkReader reader = new NetworkReader(data);
            clientObject.OnDeserialize(reader, initialState);

            int readLength = reader.Position;
            Assert.That(writeLength == readLength,
                $"OnSerializeAll and OnDeserializeAll calls write the same amount of data\n" +
                $"    writeLength={writeLength}\n" +
                $"    readLength={readLength}");
        }
    }
}
