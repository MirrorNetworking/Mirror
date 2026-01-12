using System.Collections.Generic;
using NUnit.Framework;

namespace Mirror.Tests.SyncCollections
{
    // Test SyncList Action deferral to eliminate race conditions
    public class SyncListActionTest_Deferral : MirrorTest
    {
        NetworkConnectionToClient connectionToClient;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // start server & connect client (not host)
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out connectionToClient);
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        [Test, Ignore("")]
        public void PureClient_ActionsDeferredUntilSpawnFinished()
        {
            // Track execution order
            List<string> executionOrder = new List<string>();

            // create and spawn on server with data
            CreateNetworkedAndSpawn(
                out _, out _, out SyncListDeferralBehaviour serverComp,
                out _, out _, out SyncListDeferralBehaviour clientComp,
                connectionToClient);

            // add data on server before spawn finishes on client
            serverComp.list.Add("first");
            serverComp.list.Add("second");

            // set up tracking
            clientComp.list.OnAdd += (index) => executionOrder.Add($"OnAdd:{index}");
            clientComp.onStartClientCalled = () => executionOrder.Add("OnStartClient");

            // ProcessMessages triggers spawn and deferred Actions
            ProcessMessages();

            // Actions should fire BEFORE OnStartClient
            Assert.That(executionOrder.Count, Is.GreaterThanOrEqualTo(3), "Should have OnAdd Actions + OnStartClient");
            // OnAdd Actions happen first (deferred), then OnStartClient
            Assert.That(executionOrder[0], Is.EqualTo("OnAdd:0"));
            Assert.That(executionOrder[1], Is.EqualTo("OnAdd:1"));
            Assert.That(executionOrder[2], Is.EqualTo("OnStartClient"));
        }

        [Test, Ignore("")]
        public void PureClient_MultipleCollectionsDeferred()
        {
            CreateNetworkedAndSpawn(
                out _, out _, out MultiSyncListBehaviour serverComp,
                out _, out _, out MultiSyncListBehaviour clientComp,
                connectionToClient);

            serverComp.listA.Add("A1");
            serverComp.listB.Add(42);

            List<string> executionOrder = new List<string>();
            clientComp.listA.OnAdd += (index) => executionOrder.Add($"ListA: Add:{index}");
            clientComp.listB.OnAdd += (index) => executionOrder.Add($"ListB:Add:{index}");

            ProcessMessages();

            // both Actions should fire
            Assert.That(executionOrder.Count, Is.EqualTo(2));
            Assert.That(executionOrder[0], Is.EqualTo("ListA:Add:0"));
            Assert.That(executionOrder[1], Is.EqualTo("ListB:Add: 0"));
        }

        [Test]
        public void PureClient_CrossObjectReferencesWork()
        {
            // Spawn object B first
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentityB,
                out _, out NetworkIdentity clientIdentityB,
                connectionToClient);

            // Spawn object A with SyncList<NetworkIdentity> referencing B
            CreateNetworkedAndSpawn(
                out _, out _, out SyncListIdentityBehaviour serverCompA,
                out _, out _, out SyncListIdentityBehaviour clientCompA,
                connectionToClient);

            // add reference to B
            serverCompA.identities.Add(serverIdentityB);

            // track Action - should receive valid reference
            NetworkIdentity receivedIdentity = null;
            clientCompA.identities.OnAdd += (index) =>
            {
                receivedIdentity = clientCompA.identities[index];
            };

            ProcessMessages();

            // Action should fire with correct reference (B is already spawned)
            Assert.That(receivedIdentity, Is.Not.Null, "Should receive NetworkIdentity");
            Assert.That(receivedIdentity.netId, Is.EqualTo(clientIdentityB.netId), "Should reference correct NetworkIdentity");
        }
    }

    // Test NetworkBehaviours for deferral testing
    public class SyncListDeferralBehaviour : NetworkBehaviour
    {
        public readonly SyncList<string> list = new SyncList<string>();
        public System.Action onStartClientCalled;

        public override void OnStartClient()
        {
            onStartClientCalled?.Invoke();
        }
    }

    public class SyncListIdentityBehaviour : NetworkBehaviour
    {
        public readonly SyncList<NetworkIdentity> identities = new SyncList<NetworkIdentity>();
    }

    public class MultiSyncListBehaviour : NetworkBehaviour
    {
        public readonly SyncList<string> listA = new SyncList<string>();
        public readonly SyncList<int> listB = new SyncList<int>();
    }
}