using Mirror.Tests.NetworkBehaviours;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.SyncVars
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
    class SyncVarAbstractNetworkBehaviour : NetworkBehaviour
    {
        public abstract class MockMonsterBase : NetworkBehaviour
        {
            public abstract string GetName();
        }

        public class MockZombie : MockMonsterBase
        {
            public override string GetName() => "Zombie";
        }

        public class MockWolf : MockMonsterBase
        {
            public override string GetName() => "Wolf";
        }

        [SyncVar]
        public MockMonsterBase monster1;

        [SyncVar]
        public MockMonsterBase monster2;
    }

    class SyncVarDeepInheritanceDirtyBit_0 : NetworkBehaviourSyncVarDirtyBitsExposed
    {
        [SyncVar] public int int0;
    }
    class SyncVarDeepInheritanceDirtyBit_1 : SyncVarDeepInheritanceDirtyBit_0
    {
        [SyncVar] public int int1;
    }
    class SyncVarDeepInheritanceDirtyBit_2 : SyncVarDeepInheritanceDirtyBit_1
    {
        [SyncVar] public int int2;
    }
    class SyncVarDeepInheritanceDirtyBit_3 : SyncVarDeepInheritanceDirtyBit_2
    {
        [SyncVar] public int int3;
    }

    public class SyncVarAttributeTest : MirrorTest
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // start server & connect client because we need spawn functions
            NetworkServer.Listen(1);

            // we are testing server->client syncs.
            // so we need truly separted server & client, not host.
            ConnectClientBlockingAuthenticatedAndReady(out _);
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        [Test]
        public void TestSettingStruct()
        {
            CreateNetworked(out _, out _, out MockPlayer player);

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
        public void TestSyncIntervalAndClearAllComponents()
        {
            CreateNetworked(out _, out _, out MockPlayer player);
            player.lastSyncTime = NetworkTime.localTime;
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
            player.lastSyncTime = NetworkTime.localTime - player.syncInterval;

            // should be dirty now
            Assert.That(player.IsDirty(), Is.False, "Sync interval met, should still not be dirty");
        }

        [Test]
        public void SyncsGameobject()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out SyncVarGameObject serverObject,
                out _, out _, out SyncVarGameObject clientObject);

            // create spawned because we will look up netId in .spawned
            CreateNetworkedAndSpawn(
                out GameObject serverValue, out _,
                out GameObject clientValue, out _);

            serverObject.value = serverValue;
            clientObject.value = null;

            ProcessMessages();
            Assert.That(clientObject.value, Is.EqualTo(clientValue));
        }

        [Test]
        public void SyncIdentity()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out SyncVarNetworkIdentity serverObject,
                out _, out _, out SyncVarNetworkIdentity clientObject);

            // create spawned because we will look up netId in .spawned
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverValue,
                out _, out NetworkIdentity clientValue);

            serverObject.value = serverValue;
            clientObject.value = null;

            ProcessMessages();
            Assert.That(clientObject.value, Is.EqualTo(clientValue));
        }

        [Test]
        public void SyncTransform()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out SyncVarTransform serverObject,
                out _, out _, out SyncVarTransform clientObject);

            // create spawned because we will look up netId in .spawned
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity,
                out _, out NetworkIdentity clientIdentity);

            Transform serverValue = serverIdentity.transform;
            Transform clientValue = clientIdentity.transform;

            serverObject.value = serverValue;
            clientObject.value = null;

            ProcessMessages();
            Assert.That(clientObject.value, Is.EqualTo(clientValue));
        }

        [Test]
        public void SyncsBehaviour()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out SyncVarNetworkBehaviour serverObject,
                out _, out _, out SyncVarNetworkBehaviour clientObject);

            // create spawned because we will look up netId in .spawned
            CreateNetworkedAndSpawn(
                out _, out _, out SyncVarNetworkBehaviour serverValue,
                out _, out _, out SyncVarNetworkBehaviour clientValue);

            serverObject.value = serverValue;
            clientObject.value = null;

            ProcessMessages();
            Assert.That(clientObject.value, Is.EqualTo(clientValue));
        }

        [Test]
        public void SyncsMultipleBehaviour()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out SyncVarNetworkBehaviour serverObject,
                out _, out _, out SyncVarNetworkBehaviour clientObject);

            // create spawned because we will look up netId in .spawned
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity, out SyncVarNetworkBehaviour serverBehaviour1, out SyncVarNetworkBehaviour serverBehaviour2,
                out _, out NetworkIdentity clientIdentity, out SyncVarNetworkBehaviour clientBehaviour1, out SyncVarNetworkBehaviour clientBehaviour2);

            // create array/set indices
            _ = serverIdentity.NetworkBehaviours;

            int index1 = serverBehaviour1.ComponentIndex;
            int index2 = serverBehaviour2.ComponentIndex;

            // check components of same type have different indexes
            Assert.That(index1, Is.Not.EqualTo(index2));

            // check behaviour 1 can be synced
            serverObject.value = serverBehaviour1;
            clientObject.value = null;

            ProcessMessages();
            Assert.That(clientObject.value, Is.EqualTo(clientBehaviour1));

            // check that behaviour 2 can be synced
            serverObject.value = serverBehaviour2;
            clientObject.value = null;

            ProcessMessages();
            Assert.That(clientObject.value, Is.EqualTo(clientBehaviour2));
        }

        // this test is also important if we do LocalWorldState later:
        // - if LocalWorldMessage spawns netId=N
        // - and we remove N from NetworkClient.spawned
        // - and the next LocalWorldMessage contains updated payload for N
        // =>  client should NOT assume it's a spawned payload just because the
        //     netId isn't in spawned anymore.
        [Test]
        public void SyncVarCacheNetidForGameObject()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out SyncVarGameObject serverObject,
                out _, out _, out SyncVarGameObject clientObject);

            // create spawned because we will look up netId in .spawned
            CreateNetworkedAndSpawn(
                out GameObject serverValue, out NetworkIdentity serverIdentity,
                out GameObject clientValue, out NetworkIdentity clientIdentity);

            Assert.That(serverValue, Is.Not.Null, "getCreatedValue should not return null");

            serverObject.value = serverValue;
            clientObject.value = null;

            // remove identity from client, as if it walked out of range
            NetworkClient.spawned.Remove(clientIdentity.netId);

            ProcessMessages();

            // check field shows as null
            Assert.That(clientObject.value, Is.EqualTo(null), "field should return null");

            // add identity back to collection, as if it walked back into range
            NetworkClient.spawned.Add(clientIdentity.netId, clientIdentity);

            // check field finds value
            Assert.That(clientObject.value, Is.EqualTo(clientValue), "fields should return clientValue");
        }

        // this test is also important if we do LocalWorldState later:
        // - if LocalWorldMessage spawns netId=N
        // - and we remove N from NetworkClient.spawned
        // - and the next LocalWorldMessage contains updated payload for N
        // =>  client should NOT assume it's a spawned payload just because the
        //     netId isn't in spawned anymore.
        [Test]
        public void SyncVarCacheNetidForIdentity()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out SyncVarNetworkIdentity serverObject,
                out _, out _, out SyncVarNetworkIdentity clientObject);

            // create spawned because we will look up netId in .spawned
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverValue,
                out _, out NetworkIdentity clientValue);

            Assert.That(serverValue, Is.Not.Null, "getCreatedValue should not return null");

            serverObject.value = serverValue;
            clientObject.value = null;

            // remove identity from client, as if it walked out of range
            NetworkClient.spawned.Remove(clientValue.netId);

            ProcessMessages();

            // check field shows as null
            Assert.That(clientObject.value, Is.EqualTo(null), "field should return null");

            // add identity back to collection, as if it walked back into range
            NetworkClient.spawned.Add(clientValue.netId, clientValue);

            // check field finds value
            Assert.That(clientObject.value, Is.EqualTo(clientValue), "fields should return clientValue");
        }

        // this test is also important if we do LocalWorldState later:
        // - if LocalWorldMessage spawns netId=N
        // - and we remove N from NetworkClient.spawned
        // - and the next LocalWorldMessage contains updated payload for N
        // =>  client should NOT assume it's a spawned payload just because the
        //     netId isn't in spawned anymore.
        [Test]
        public void SyncVarCacheNetidForBehaviour()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out SyncVarNetworkBehaviour serverObject,
                out _, out _, out SyncVarNetworkBehaviour clientObject);

            // create spawned because we will look up netId in .spawned
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity, out SyncVarNetworkBehaviour serverValue,
                out _, out NetworkIdentity clientIdentity, out SyncVarNetworkBehaviour clientValue);

            Assert.That(serverValue, Is.Not.Null, "getCreatedValue should not return null");

            // set on server
            serverObject.value = serverValue;
            clientObject.value = null;

            // remove identity from client, as if it walked out of range
            NetworkClient.spawned.Remove(clientIdentity.netId);

            ProcessMessages();

            // check field shows as null
            Assert.That(clientObject.value, Is.EqualTo(null), "field should return null");

            // add identity back to collection, as if it walked back into range
            NetworkClient.spawned.Add(clientIdentity.netId, clientIdentity);

            // check field finds value
            Assert.That(clientObject.value, Is.EqualTo(clientValue), "fields should return clientValue");
        }

        [Test]
        public void TestSyncingAbstractNetworkBehaviour()
        {
            // set up a "server" object
            CreateNetworked(out _, out NetworkIdentity serverIdentity, out SyncVarAbstractNetworkBehaviour serverBehaviour);
            serverIdentity.isServer = true;

            // spawn syncvar targets
            CreateNetworked(out _, out NetworkIdentity wolfIdentity, out SyncVarAbstractNetworkBehaviour.MockWolf wolf);
            CreateNetworked(out _, out NetworkIdentity zombieIdentity, out SyncVarAbstractNetworkBehaviour.MockZombie zombie);

            wolfIdentity.netId = 135;
            zombieIdentity.netId = 246;

            // add to spawned as if they were spawned on clients
            NetworkClient.spawned.Add(wolfIdentity.netId, wolfIdentity);
            NetworkClient.spawned.Add(zombieIdentity.netId, zombieIdentity);

            serverBehaviour.monster1 = wolf;
            serverBehaviour.monster2 = zombie;

            // serialize all the data as we would for the network
            NetworkWriter ownerWriter = new NetworkWriter();
            // not really used in this Test
            NetworkWriter observersWriter = new NetworkWriter();
            serverIdentity.SerializeServer_Spawn(ownerWriter, observersWriter);

            // set up a "client" object
            CreateNetworked(out _, out NetworkIdentity clientIdentity, out SyncVarAbstractNetworkBehaviour clientBehaviour);
            clientIdentity.isClient = true;

            // apply all the data from the server object
            NetworkReader reader = new NetworkReader(ownerWriter.ToArray());
            clientIdentity.DeserializeClient(reader, true);

            // check that the syncvars got updated
            Debug.Log($"{clientBehaviour.monster1} and {serverBehaviour.monster1}");
            Assert.That(clientBehaviour.monster1, Is.EqualTo(serverBehaviour.monster1), "Data should be synchronized");
            Assert.That(clientBehaviour.monster2, Is.EqualTo(serverBehaviour.monster2), "Data should be synchronized");

            // remove spawned objects
            NetworkClient.spawned.Remove(wolfIdentity.netId);
            NetworkClient.spawned.Remove(zombieIdentity.netId);
        }

        // Tests if getter for GameObject SyncVar field returns proper value on server before the containing object is spawned.
        [Test]
        public void SyncVarGameObjectGetterOnServerBeforeSpawn()
        {
            // The test should only need server objects, but at the same time this belongs in SyncVar tests,
            // and objects in the tests defined here need client objects to spawn.
            CreateNetworkedAndSpawn(
                out GameObject serverGO, out NetworkIdentity serverIdentity, out SyncVarNetworkBehaviour serverNB,
                out _, out _, out _);

            CreateNetworked(out _, out _, out SyncVarGameObject serverComponent);

            serverComponent.value = serverGO;
            Assert.That(serverComponent.value, Is.EqualTo(serverGO), "getter should return original field value on server");
        }

        [Test]
        public void SyncVarNetworkIdentityGetterOnServerBeforeSpawn()
        {
            CreateNetworkedAndSpawn(
                out GameObject serverGO, out NetworkIdentity serverIdentity, out SyncVarNetworkBehaviour serverNB,
                out _, out _, out _);

            CreateNetworked(out _, out _, out SyncVarNetworkIdentity serverComponent);

            serverComponent.value = serverIdentity;
            Assert.That(serverComponent.value, Is.EqualTo(serverIdentity), "getter should return original field value on server");
        }

        [Test]
        public void SyncVarNetworkBehaviourGetterOnServerBeforeSpawn()
        {
            CreateNetworkedAndSpawn(
                out GameObject serverGO, out NetworkIdentity serverIdentity, out SyncVarNetworkBehaviour serverNB,
                out _, out _, out _);

            CreateNetworked(out _, out _, out SyncVarNetworkBehaviour serverComponent);

            serverComponent.value = serverNB;
            Assert.That(serverComponent.value, Is.EqualTo(serverNB), "getter should return original field value on server");
        }

        // test for https://github.com/MirrorNetworking/Mirror/issues/3457
        [Test]
        public void DeepInheritanceSyncVarDirtyBitUniqueness()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out SyncVarDeepInheritanceDirtyBit_3 serverObject,
                out _, out _, out _);

            // test SyncVar dirty bits

            ulong lastSyncVarDirtyBits = serverObject.syncVarDirtyBitsExposed;
            void AssertUniqueSyncVarDirtyBit()
            {
                // check if dirty mask has changed
                Assert.That(lastSyncVarDirtyBits, Is.Not.EqualTo(serverObject.syncVarDirtyBitsExposed), "every SyncVar dirty bit should be unique");
                lastSyncVarDirtyBits = serverObject.syncVarDirtyBitsExposed;
            }

            serverObject.int0 = 1234;
            AssertUniqueSyncVarDirtyBit();

            serverObject.int1 = 2345;
            AssertUniqueSyncVarDirtyBit();

            serverObject.int2 = 3456;
            AssertUniqueSyncVarDirtyBit();

            serverObject.int3 = 4567;
            AssertUniqueSyncVarDirtyBit();
        }
    }
}
