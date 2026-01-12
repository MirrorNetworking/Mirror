using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.SyncCollections
{
    public class SyncDictionaryActionTest_HostMode : MirrorTest
    {
        DistanceInterestManagement aoi;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            aoi = holder.AddComponent<DistanceInterestManagement>();
            aoi.visRange = 10;
            NetworkServer.aoi = aoi;

            NetworkServer.Listen(1);
            ConnectHostClientBlockingAuthenticatedAndReady();
        }

        [TearDown]
        public override void TearDown()
        {
            NetworkServer.aoi = null;
            base.TearDown();
        }

        [Test]
        public void HostMode_ActionNotCalledForOutOfRangeObject()
        {
            CreateNetworkedAndSpawn(out _, out _, out SyncDictionaryHostModeBehaviour comp);
            comp.transform.position = Vector3.right * (aoi.visRange + 1);
            NetworkServer.RebuildObservers(comp.netIdentity, true);

            int actionCallCount = 0;
            comp.dict.OnAdd += (key) => actionCallCount++;

            comp.dict.Add(1, "test");

            Assert.That(actionCallCount, Is.EqualTo(0));
        }

        [Test]
        public void HostMode_ActionsCalledWhenEnteringRange()
        {
            // Use the same pattern as existing SyncDictionary tests
            // Create separate server and client dictionaries
            SyncDictionary<int, string> serverDict = new SyncDictionary<int, string>();
            SyncDictionary<int, string> clientDict = new SyncDictionary<int, string>();

            // Create a mock NetworkBehaviour to set the networkBehaviour reference
            CreateNetworked(out _, out NetworkIdentity serverIdentity, out SyncDictionaryHostModeBehaviour serverComp);
            CreateNetworked(out _, out NetworkIdentity clientIdentity, out SyncDictionaryHostModeBehaviour clientComp);

            // Set up the dictionaries' networkBehaviour references
            serverDict.networkBehaviour = serverComp;
            clientDict.networkBehaviour = clientComp;

            // Set writable/recording
            serverDict.IsWritable = () => true;
            serverDict.IsRecording = () => true;
            clientDict.IsWritable = () => false;

            // Simulate host mode for client side
            //NetworkServer.SetLocalConnection( new LocalConnectionToClient());

            // Server is not in client spawned (out of range)
            Assert.That(NetworkClient.spawned.ContainsKey(clientIdentity.netId), Is.False);

            // Add items on server - Actions should NOT fire because out of range
            serverDict.Add(1, "test");
            serverDict.Add(2, "test2");

            // Track Actions on client
            int addCallCount = 0;
            clientDict.OnAdd += (key) => addCallCount++;

            // Simulate entering range: add to spawned
            NetworkClient.spawned[clientIdentity.netId] = clientIdentity;
            clientIdentity.hostInitialSpawn = true;

            // Serialize from server to client
            NetworkWriter writer = new NetworkWriter();
            serverDict.OnSerializeDelta(writer);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            clientDict.OnDeserializeDelta(reader);

            clientIdentity.hostInitialSpawn = false;
            //NetworkServer.RemoveLocalConnection();

            Assert.That(addCallCount, Is.EqualTo(2), "Both Add Actions should fire when entering range");
        }
    }

    public class SyncDictionaryHostModeBehaviour : NetworkBehaviour
    {
        public readonly SyncDictionary<int, string> dict = new SyncDictionary<int, string>();
    }
}