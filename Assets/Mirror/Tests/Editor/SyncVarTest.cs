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

            GameObject gameObject = new GameObject();

            MockPlayer player = gameObject.AddComponent<MockPlayer>();

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

            GameObject gameObject = new GameObject();

            MockPlayer player = gameObject.AddComponent<MockPlayer>();
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

            GameObject gameObject = new GameObject();

            MockPlayer player = gameObject.AddComponent<MockPlayer>();
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
            GameObject gameObject1 = new GameObject();
            NetworkIdentity identity1 = gameObject1.AddComponent<NetworkIdentity>();
            MockPlayer player1 = gameObject1.AddComponent<MockPlayer>();
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
            GameObject gameObject2 = new GameObject();
            NetworkIdentity identity2 = gameObject2.AddComponent<NetworkIdentity>();
            MockPlayer player2 = gameObject2.AddComponent<MockPlayer>();

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
            SyncVarGameObject serverObject = CreateObject<SyncVarGameObject>();
            SyncVarGameObject clientObject = CreateObject<SyncVarGameObject>();

            GameObject serverValue = CreateNetworkIdentity(2044).gameObject;

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
            SyncVarNetworkIdentity serverObject = CreateObject<SyncVarNetworkIdentity>();
            SyncVarNetworkIdentity clientObject = CreateObject<SyncVarNetworkIdentity>();

            NetworkIdentity serverValue = CreateNetworkIdentity(2045);

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
            SyncVarTransform serverObject = CreateObject<SyncVarTransform>();
            SyncVarTransform clientObject = CreateObject<SyncVarTransform>();

            Transform serverValue = CreateNetworkIdentity(2045).transform;

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
            SyncVarNetworkBehaviour serverObject = CreateObject<SyncVarNetworkBehaviour>();
            SyncVarNetworkBehaviour clientObject = CreateObject<SyncVarNetworkBehaviour>();

            SyncVarNetworkBehaviour serverValue = CreateNetworkIdentity(2046).gameObject.AddComponent<SyncVarNetworkBehaviour>();

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
            SyncVarNetworkBehaviour serverObject = CreateObject<SyncVarNetworkBehaviour>();
            SyncVarNetworkBehaviour clientObject = CreateObject<SyncVarNetworkBehaviour>();

            NetworkIdentity identity = CreateNetworkIdentity(2046);
            SyncVarNetworkBehaviour behaviour1 = identity.gameObject.AddComponent<SyncVarNetworkBehaviour>();
            SyncVarNetworkBehaviour behaviour2 = identity.gameObject.AddComponent<SyncVarNetworkBehaviour>();
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
            SyncVarCacheNetidForGeneric<SyncVarGameObject, GameObject>(
                (obj) => obj.value,
                (obj, value) => obj.value = value,
                (identity) => identity.gameObject,
                initialState
            );
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void SyncVarCacheNetidForIdentity(bool initialState)
        {
            SyncVarCacheNetidForGeneric<SyncVarNetworkIdentity, NetworkIdentity>(
                (obj) => obj.value,
                (obj, value) => obj.value = value,
                (identity) => identity,
                initialState
            );
        }

        [Test]
        [TestCase(true)]
        [TestCase(false)]
        [Ignore("Transform backing field has not been implemented yet")]
        public void SyncVarCacheNetidForTransform(bool initialState)
        {
            SyncVarCacheNetidForGeneric<SyncVarTransform, Transform>(
                (obj) => obj.value,
                (obj, value) => obj.value = value,
                (identity) => identity.transform,
                initialState
            );
        }
        [Test]
        [TestCase(true)]
        [TestCase(false)]
        public void SyncVarCacheNetidForBehaviour(bool initialState)
        {
            SyncVarCacheNetidForGeneric<SyncVarNetworkBehaviour, SyncVarNetworkBehaviour>(
                (obj) => obj.value,
                (obj, value) => obj.value = value,
                (identity) => identity.gameObject.AddComponent<SyncVarNetworkBehaviour>(),
                initialState
            );
        }

        void SyncVarCacheNetidForGeneric<TBehaviour, TValue>(
            Func<TBehaviour, TValue> getField,
            Action<TBehaviour, TValue> setField,
            Func<NetworkIdentity, TValue> getCreatedValue,
            bool initialState)
            where TValue : UnityEngine.Object
            where TBehaviour : NetworkBehaviour
        {
            TBehaviour serverObject = CreateObject<TBehaviour>();
            TBehaviour clientObject = CreateObject<TBehaviour>();

            NetworkIdentity identity = CreateNetworkIdentity(2047);
            TValue serverValue = getCreatedValue(identity);

            Assert.That(serverValue, Is.Not.Null, "getCreatedValue should not return null");

            setField(serverObject, serverValue);
            setField(clientObject, null);

            // write server data
            bool written = ServerWrite(serverObject, initialState, out ArraySegment<byte> data, out int writeLength);
            Assert.IsTrue(written, "did not write");

            // remove identity from collection
            NetworkIdentity.spawned.Remove(identity.netId);

            // read client data, this should be cached in field
            ClientRead(clientObject, initialState, data, writeLength);

            // check field shows as null
            Assert.That(getField(clientObject), Is.EqualTo(null), "field should return null");

            // add identity back to collection
            NetworkIdentity.spawned.Add(identity.netId, identity);

            // check field finds value
            Assert.That(getField(clientObject), Is.EqualTo(serverValue), "fields should return serverValue");
        }
    }
}
