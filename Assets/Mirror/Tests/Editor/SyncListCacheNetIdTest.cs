using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class SyncListCacheNetIdTest : MirrorEditModeTest
    {
        public static void SerializeAllTo<T>(T fromList, T toList) where T : SyncObject
        {
            NetworkWriter writer = new NetworkWriter();
            fromList.OnSerializeAll(writer);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            toList.OnDeserializeAll(reader);

            int writeLength = writer.Position;
            int readLength = reader.Position;
            Assert.That(writeLength == readLength, $"OnSerializeAll and OnDeserializeAll calls write the same amount of data\n    writeLength={writeLength}\n    readLength={readLength}");

        }

        class DerivedBehaviour : NetworkBehaviour { }

        [Test]
        public void SyncListCacheNetIdForNetworkIdentity()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity oldA);
            oldA.netId = 1001;
            NetworkIdentity.spawned[oldA.netId] = oldA;

            CreateNetworked(out GameObject _, out NetworkIdentity oldB);
            oldB.netId = 1002;
            NetworkIdentity.spawned[oldB.netId] = oldB;

            var serverList = new SyncList<NetworkIdentity>();
            var clientList = new SyncList<NetworkIdentity>();

            serverList.Add(oldA);
            serverList.Add(oldB);

            SerializeAllTo(serverList, clientList);

            // check if client points to right value
            Assert.That(clientList, Is.EquivalentTo(new[] { oldA, oldB }), "Should be synced correctly");

            // objects hidden from client
            NetworkIdentity.spawned.Remove(oldA.netId);
            NetworkIdentity.spawned.Remove(oldB.netId);

            // check if client points to null
            Assert.That(clientList, Is.EquivalentTo(new NetworkIdentity[] { null, null }), "Should point to null for hidden objects");

            // objects shown to client
            CreateNetworked(out GameObject _, out NetworkIdentity newA);
            newA.netId = 1001;
            NetworkIdentity.spawned[newA.netId] = newA;

            CreateNetworked(out GameObject _, out NetworkIdentity newB);
            newB.netId = 1002;
            NetworkIdentity.spawned[newB.netId] = newB;

            // check if client points to new objects
            Assert.That(clientList, Is.EquivalentTo(new[] { newA, newB }), "Should point to new objects");
        }

        [Test]
        public void SyncListCacheNetIdForGameObject()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity oldA);
            oldA.netId = 1001;
            NetworkIdentity.spawned[oldA.netId] = oldA;

            CreateNetworked(out GameObject _, out NetworkIdentity oldB);
            oldB.netId = 1002;
            NetworkIdentity.spawned[oldB.netId] = oldB;

            var serverList = new SyncList<GameObject>();
            var clientList = new SyncList<GameObject>();

            serverList.Add(oldA.gameObject);
            serverList.Add(oldB.gameObject);

            SerializeAllTo(serverList, clientList);

            // check if client points to right value
            Assert.That(clientList, Is.EquivalentTo(new[] { oldA.gameObject, oldB.gameObject }), "Should be synced correctly");

            // objects hidden from client
            NetworkIdentity.spawned.Remove(oldA.netId);
            NetworkIdentity.spawned.Remove(oldB.netId);

            // check if client points to null
            Assert.That(clientList, Is.EquivalentTo(new NetworkIdentity[] { null, null }), "Should point to null for hidden objects");

            // objects shown to client
            CreateNetworked(out GameObject _, out NetworkIdentity newA);
            newA.netId = 1001;
            NetworkIdentity.spawned[newA.netId] = newA;

            CreateNetworked(out GameObject _, out NetworkIdentity newB);
            newB.netId = 1002;
            NetworkIdentity.spawned[newB.netId] = newB;

            // check if client points to new objects
            Assert.That(clientList, Is.EquivalentTo(new[] { newA.gameObject, newB.gameObject }), "Should point to new objects");
        }

        [Test]
        public void SyncListCacheNetIdForNetworkBehaviour()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity _, out DerivedBehaviour oldA);
            oldA.netIdentity.netId = 1001;
            NetworkIdentity.spawned[oldA.netId] = oldA.netIdentity;

            CreateNetworked(out GameObject _, out NetworkIdentity _, out DerivedBehaviour oldB);
            oldB.netIdentity.netId = 1002;
            NetworkIdentity.spawned[oldB.netId] = oldB.netIdentity;

            var serverList = new SyncList<DerivedBehaviour>();
            var clientList = new SyncList<DerivedBehaviour>();

            serverList.Add(oldA);
            serverList.Add(oldB);

            SerializeAllTo(serverList, clientList);

            // check if client points to right value
            Assert.That(clientList, Is.EquivalentTo(new[] { oldA, oldB }), "Should be synced correctly");

            // objects hidden from client
            NetworkIdentity.spawned.Remove(oldA.netId);
            NetworkIdentity.spawned.Remove(oldB.netId);

            // check if client points to null
            Assert.That(clientList, Is.EquivalentTo(new NetworkIdentity[] { null, null }), "Should point to null for hidden objects");

            // objects shown to client
            CreateNetworked(out GameObject _, out NetworkIdentity _, out DerivedBehaviour newA);
            newA.netIdentity.netId = 1001;
            NetworkIdentity.spawned[newA.netId] = newA.netIdentity;

            CreateNetworked(out GameObject _, out NetworkIdentity _, out DerivedBehaviour newB);
            newB.netIdentity.netId = 1002;
            NetworkIdentity.spawned[newB.netId] = newB.netIdentity;

            // check if client points to new objects
            Assert.That(clientList, Is.EquivalentTo(new[] { newA, newB }), "Should point to new objects");
        }
    }
}
