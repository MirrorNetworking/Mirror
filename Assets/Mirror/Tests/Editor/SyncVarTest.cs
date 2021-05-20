using System;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.SyncVarTests
{
    class MockPlayer : NetworkBehaviour
    {
        public struct Guild
        {
            public string name;
        }

        [SyncVar]
        public Guild guild;
    }

    class SyncVarGameObject : NetworkBehaviour
    {
        [SyncVar]
        public GameObject value;
    }
    class SyncVarNetworkIdentity : NetworkBehaviour
    {
        [SyncVar]
        public NetworkIdentity value;
    }
    class SyncVarTransform : NetworkBehaviour
    {
        [SyncVar]
        public Transform value;
    }
    class SyncVarNetworkBehaviour : NetworkBehaviour
    {
        [SyncVar]
        public SyncVarNetworkBehaviour value;
    }

    public class SyncVarTest : SyncVarTestBase
    {
        [Test]
        public void TestSettingStruct()
        {
            CreateNetworked(out GameObject gameObject, out NetworkIdentity identity, out MockPlayer player);

            // synchronize immediately
            player.syncInterval = 0f;

            Assert.That(player.IsDirty(), Is.False, "First time object should not be dirty");

            MockPlayer.Guild myGuild = new MockPlayer.Guild
            {
                name = "Back street boys"
            };

            player.guild = myGuild;

            Assert.That(player.IsDirty(), "Setting struct should mark object as dirty");
            player.ClearAllDirtyBits();
            Assert.That(player.IsDirty(), Is.False, "ClearAllDirtyBits() should clear dirty flag");

            // clearing the guild should set dirty bit too
            player.guild = default;
            Assert.That(player.IsDirty(), "Clearing struct should mark object as dirty");
        }

        [Test]
        public void TestSyncIntervalAndClearDirtyComponents()
        {
            CreateNetworked(out GameObject gameObject, out NetworkIdentity identity, out MockPlayer player);
            player.lastSyncTime = Time.time;
            // synchronize immediately
            player.syncInterval = 1f;

            player.guild = new MockPlayer.Guild
            {
                name = "Back street boys"
            };

            Assert.That(player.IsDirty(), Is.False, "Sync interval not met, so not dirty yet");

            // ClearDirtyComponents should do nothing since syncInterval is not
            // elapsed yet
            player.netIdentity.ClearDirtyComponentsDirtyBits();

            // set lastSyncTime far enough back to be ready for syncing
            player.lastSyncTime = Time.time - player.syncInterval;

            // should be dirty now
            Assert.That(player.IsDirty(), Is.True, "Sync interval met, should be dirty");
        }

        [Test]
        public void TestSyncIntervalAndClearAllComponents()
        {
            CreateNetworked(out GameObject gameObject, out NetworkIdentity identity, out MockPlayer player);
            player.lastSyncTime = Time.time;
            // synchronize immediately
            player.syncInterval = 1f;

            player.guild = new MockPlayer.Guild
            {
                name = "Back street boys"
            };

            Assert.That(player.IsDirty(), Is.False, "Sync interval not met, so not dirty yet");

            // ClearAllComponents should clear dirty even if syncInterval not
            // elapsed yet
            player.netIdentity.ClearAllComponentsDirtyBits();

            // set lastSyncTime far enough back to be ready for syncing
            player.lastSyncTime = Time.time - player.syncInterval;

            // should be dirty now
            Assert.That(player.IsDirty(), Is.False, "Sync interval met, should still not be dirty");
        }

        [Test]
        public void TestSynchronizingObjects()
        {
            // set up a "server" object
            CreateNetworked(out GameObject gameObject1, out NetworkIdentity identity1, out MockPlayer player1);
            MockPlayer.Guild myGuild = new MockPlayer.Guild
            {
                name = "Back street boys"
            };
            player1.guild = myGuild;

            // serialize all the data as we would for the network
            NetworkWriter ownerWriter = new NetworkWriter();
            // not really used in this Test
            NetworkWriter observersWriter = new NetworkWriter();
            identity1.OnSerializeAllSafely(true, ownerWriter, out int ownerWritten, observersWriter, out int observersWritten);

            // set up a "client" object
            CreateNetworked(out GameObject gameObject2, out NetworkIdentity identity2, out MockPlayer player2);

            // apply all the data from the server object
            NetworkReader reader = new NetworkReader(ownerWriter.ToArray());
            identity2.OnDeserializeAllSafely(reader, true);

            // check that the syncvars got updated
            Assert.That(player2.guild.name, Is.EqualTo("Back street boys"), "Data should be synchronized");
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void SyncsGameobject(bool initialState)
        {
            CreateNetworked(out GameObject _, out NetworkIdentity _, out SyncVarGameObject serverObject);
            CreateNetworked(out GameObject _, out NetworkIdentity _, out SyncVarGameObject clientObject);

            CreateNetworked(out GameObject serverValue, out NetworkIdentity serverIdentity);
            serverIdentity.netId = 2044;
            NetworkIdentity.spawned[serverIdentity.netId] = serverIdentity;

            serverObject.value = serverValue;
            clientObject.value = null;

            bool written = SyncToClient(serverObject, clientObject, initialState);
            Assert.IsTrue(written);
            Assert.That(clientObject.value, Is.EqualTo(serverValue));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void SyncIdentity(bool initialState)
        {
            CreateNetworked(out GameObject _, out NetworkIdentity _, out SyncVarNetworkIdentity serverObject);
            CreateNetworked(out GameObject _, out NetworkIdentity _, out SyncVarNetworkIdentity clientObject);

            CreateNetworked(out GameObject _, out NetworkIdentity serverValue);
            serverValue.netId = 2045;
            NetworkIdentity.spawned[serverValue.netId] = serverValue;

            serverObject.value = serverValue;
            clientObject.value = null;

            bool written = SyncToClient(serverObject, clientObject, initialState);
            Assert.IsTrue(written);
            Assert.That(clientObject.value, Is.EqualTo(serverValue));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void SyncTransform(bool initialState)
        {
            CreateNetworked(out GameObject _, out NetworkIdentity _, out SyncVarTransform serverObject);
            CreateNetworked(out GameObject _, out NetworkIdentity _, out SyncVarTransform clientObject);

            CreateNetworked(out GameObject _, out NetworkIdentity serverIdentity);
            serverIdentity.netId = 2045;
            NetworkIdentity.spawned[serverIdentity.netId] = serverIdentity;
            Transform serverValue = serverIdentity.transform;

            serverObject.value = serverValue;
            clientObject.value = null;

            bool written = SyncToClient(serverObject, clientObject, initialState);
            Assert.IsTrue(written);
            Assert.That(clientObject.value, Is.EqualTo(serverValue));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void SyncsBehaviour(bool initialState)
        {
            CreateNetworked(out GameObject _, out NetworkIdentity _, out SyncVarNetworkBehaviour serverObject);
            CreateNetworked(out GameObject _, out NetworkIdentity _, out SyncVarNetworkBehaviour clientObject);

            CreateNetworked(out GameObject _, out NetworkIdentity serverIdentity, out SyncVarNetworkBehaviour serverValue);
            serverIdentity.netId = 2046;
            NetworkIdentity.spawned[serverIdentity.netId] = serverIdentity;

            serverObject.value = serverValue;
            clientObject.value = null;

            bool written = SyncToClient(serverObject, clientObject, initialState);
            Assert.IsTrue(written);
            Assert.That(clientObject.value, Is.EqualTo(serverValue));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void SyncsMultipleBehaviour(bool initialState)
        {
            CreateNetworked(out GameObject _, out NetworkIdentity _, out SyncVarNetworkBehaviour serverObject);
            CreateNetworked(out GameObject _, out NetworkIdentity _, out SyncVarNetworkBehaviour clientObject);

            CreateNetworked(out GameObject _, out NetworkIdentity identity, out SyncVarNetworkBehaviour behaviour1, out SyncVarNetworkBehaviour behaviour2);
            identity.netId = 2046;
            NetworkIdentity.spawned[identity.netId] = identity;

            // create array/set indices
            _ = identity.NetworkBehaviours;

            int index1 = behaviour1.ComponentIndex;
            int index2 = behaviour2.ComponentIndex;

            // check components of same type have different indexes
            Assert.That(index1, Is.Not.EqualTo(index2));

            // check behaviour 1 can be synced
            serverObject.value = behaviour1;
            clientObject.value = null;

            bool written1 = SyncToClient(serverObject, clientObject, initialState);
            Assert.IsTrue(written1);
            Assert.That(clientObject.value, Is.EqualTo(behaviour1));

            // check that behaviour 2 can be synced
            serverObject.value = behaviour2;
            clientObject.value = null;

            bool written2 = SyncToClient(serverObject, clientObject, initialState);
            Assert.IsTrue(written2);
            Assert.That(clientObject.value, Is.EqualTo(behaviour2));
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void SyncVarCacheNetidForGameObject(bool initialState)
        {
            CreateNetworked(out GameObject _, out NetworkIdentity _, out SyncVarGameObject serverObject);
            CreateNetworked(out GameObject _, out NetworkIdentity _, out SyncVarGameObject clientObject);

            CreateNetworked(out GameObject serverValue, out NetworkIdentity identity);
            identity.netId = 2047;
            NetworkIdentity.spawned[identity.netId] = identity;

            Assert.That(serverValue, Is.Not.Null, "getCreatedValue should not return null");

            serverObject.value = serverValue;
            clientObject.value = null;

            // write server data
            bool written = ServerWrite(serverObject, initialState, out ArraySegment<byte> data, out int writeLength);
            Assert.IsTrue(written, "did not write");

            // remove identity from collection
            NetworkIdentity.spawned.Remove(identity.netId);

            // read client data, this should be cached in field
            ClientRead(clientObject, initialState, data, writeLength);

            // check field shows as null
            Assert.That(clientObject.value, Is.EqualTo(null), "field should return null");

            // add identity back to collection
            NetworkIdentity.spawned.Add(identity.netId, identity);

            // check field finds value
            Assert.That(clientObject.value, Is.EqualTo(serverValue), "fields should return serverValue");
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void SyncVarCacheNetidForIdentity(bool initialState)
        {
            CreateNetworked(out GameObject _, out NetworkIdentity _, out SyncVarNetworkIdentity serverObject);
            CreateNetworked(out GameObject _, out NetworkIdentity _, out SyncVarNetworkIdentity clientObject);

            CreateNetworked(out GameObject _, out NetworkIdentity serverValue);
            serverValue.netId = 2047;
            NetworkIdentity.spawned[serverValue.netId] = serverValue;

            Assert.That(serverValue, Is.Not.Null, "getCreatedValue should not return null");

            serverObject.value = serverValue;
            clientObject.value = null;

            // write server data
            bool written = ServerWrite(serverObject, initialState, out ArraySegment<byte> data, out int writeLength);
            Assert.IsTrue(written, "did not write");

            // remove identity from collection
            NetworkIdentity.spawned.Remove(serverValue.netId);

            // read client data, this should be cached in field
            ClientRead(clientObject, initialState, data, writeLength);

            // check field shows as null
            Assert.That(clientObject.value, Is.EqualTo(null), "field should return null");

            // add identity back to collection
            NetworkIdentity.spawned.Add(serverValue.netId, serverValue);

            // check field finds value
            Assert.That(clientObject.value, Is.EqualTo(serverValue), "fields should return serverValue");
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void SyncVarCacheNetidForBehaviour(bool initialState)
        {
            CreateNetworked(out GameObject _, out NetworkIdentity _, out SyncVarNetworkBehaviour serverObject);
            CreateNetworked(out GameObject _, out NetworkIdentity _, out SyncVarNetworkBehaviour clientObject);

            CreateNetworked(out GameObject _, out NetworkIdentity identity, out SyncVarNetworkBehaviour serverValue);
            identity.netId = 2047;
            NetworkIdentity.spawned[identity.netId] = identity;

            Assert.That(serverValue, Is.Not.Null, "getCreatedValue should not return null");

            serverObject.value = serverValue;
            clientObject.value = null;

            // write server data
            bool written = ServerWrite(serverObject, initialState, out ArraySegment<byte> data, out int writeLength);
            Assert.IsTrue(written, "did not write");

            // remove identity from collection
            NetworkIdentity.spawned.Remove(identity.netId);

            // read client data, this should be cached in field
            ClientRead(clientObject, initialState, data, writeLength);

            // check field shows as null
            Assert.That(clientObject.value, Is.EqualTo(null), "field should return null");

            // add identity back to collection
            NetworkIdentity.spawned.Add(identity.netId, identity);

            // check field finds value
            Assert.That(clientObject.value, Is.EqualTo(serverValue), "fields should return serverValue");
        }
    }
}
