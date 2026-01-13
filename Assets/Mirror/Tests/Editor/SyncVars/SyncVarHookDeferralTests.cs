using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.SyncVars
{
    public class SyncVarHookDeferralTests : MirrorEditModeTest
    {
        [Test]
        public void CrossReferences_HooksCanAccessAllTargets()
        {
            // This tests the core fix:  hooks are deferred until all objects are in spawned
            NetworkServer.Listen(10);
            ConnectClientBlockingAuthenticatedAndReady(out _);

            // Create 3 objects with cross-references on BOTH server and client
            // (simulates the scenario where multiple objects spawn together)
            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity1, out CrossRefBehaviour serverComp1,
                out _, out NetworkIdentity clientIdentity1, out CrossRefBehaviour clientComp1);

            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity2, out CrossRefBehaviour serverComp2,
                out _, out NetworkIdentity clientIdentity2, out CrossRefBehaviour clientComp2);

            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity3, out CrossRefBehaviour serverComp3,
                out _, out NetworkIdentity clientIdentity3, out CrossRefBehaviour clientComp3);

            // Set circular cross-references on server:  1->2, 2->3, 3->1
            serverComp1.target = serverIdentity2;
            serverComp2.target = serverIdentity3;
            serverComp3.target = serverIdentity1;

            // Sync to client
            ProcessMessages();

            // Verify hooks fired
            Assert.That(clientComp1.callCount, Is.EqualTo(1), "Hook 1 should fire");
            Assert.That(clientComp2.callCount, Is.EqualTo(1), "Hook 2 should fire");
            Assert.That(clientComp3.callCount, Is.EqualTo(1), "Hook 3 should fire");

            // THE CRITICAL TEST: All targets were accessible during hook execution
            // Without deferred hooks, some would be false (race condition)
            Assert.That(clientComp1.targetWasInSpawnedWhenHookFired, Is.True,
                "Target 2 should be in spawned when hook 1 fires - THIS IS THE FIX!");
            Assert.That(clientComp2.targetWasInSpawnedWhenHookFired, Is.True,
                "Target 3 should be in spawned when hook 2 fires - THIS IS THE FIX!");
            Assert.That(clientComp3.targetWasInSpawnedWhenHookFired, Is.True,
                "Target 1 should be in spawned when hook 3 fires - THIS IS THE FIX!");

            // Verify references are correct
            Assert.That(clientComp1.target, Is.EqualTo(clientIdentity2));
            Assert.That(clientComp2.target, Is.EqualTo(clientIdentity3));
            Assert.That(clientComp3.target, Is.EqualTo(clientIdentity1));
        }

        [Test]
        public void GameObject_CrossReferences_HooksCanAccessTargets()
        {
            NetworkServer.Listen(10);
            ConnectClientBlockingAuthenticatedAndReady(out _);

            // Create objects with GameObject SyncVar cross-references
            CreateNetworkedAndSpawn(
                out _, out _, out GameObjectCrossRefBehaviour serverComp1,
                out _, out _, out GameObjectCrossRefBehaviour clientComp1);

            CreateNetworkedAndSpawn(
                out GameObject serverGO2, out _, out GameObjectCrossRefBehaviour _,
                out GameObject clientGO2, out _, out GameObjectCrossRefBehaviour _);

            CreateNetworkedAndSpawn(
                out GameObject serverGO3, out _, out GameObjectCrossRefBehaviour _,
                out GameObject clientGO3, out _, out GameObjectCrossRefBehaviour _);

            // Set circular GameObject references:  1->2, 2->3, 3->1
            serverComp1.targetGO = serverGO2;

            var serverComp2 = serverGO2.GetComponent<GameObjectCrossRefBehaviour>();
            serverComp2.targetGO = serverGO3;

            var serverComp3 = serverGO3.GetComponent<GameObjectCrossRefBehaviour>();
            serverComp3.targetGO = serverComp1.gameObject;

            ProcessMessages();

            var clientComp2 = clientGO2.GetComponent<GameObjectCrossRefBehaviour>();
            var clientComp3 = clientGO3.GetComponent<GameObjectCrossRefBehaviour>();

            // Verify hooks fired
            Assert.That(clientComp1.callCount, Is.EqualTo(1));
            Assert.That(clientComp2.callCount, Is.EqualTo(1));
            Assert.That(clientComp3.callCount, Is.EqualTo(1));

            // Critical: GameObject targets were accessible during hooks
            Assert.That(clientComp1.targetWasInSpawnedWhenHookFired, Is.True,
                "GameObject target should be spawned when hook fires");
            Assert.That(clientComp2.targetWasInSpawnedWhenHookFired, Is.True,
                "GameObject target should be spawned when hook fires");
            Assert.That(clientComp3.targetWasInSpawnedWhenHookFired, Is.True,
                "GameObject target should be spawned when hook fires");

            // Verify references are correct
            Assert.That(clientComp1.targetGO, Is.EqualTo(clientGO2));
            Assert.That(clientComp2.targetGO, Is.EqualTo(clientGO3));
            Assert.That(clientComp3.targetGO, Is.EqualTo(clientComp1.gameObject));
        }

        [Test]
        public void HostMode_CrossReferences_HooksFireFromSetter_NotDeferred()
        {
            // Start in HOST mode (server + client together)
            NetworkServer.Listen(10);
            ConnectHostClientBlockingAuthenticatedAndReady();

            Assert.That(NetworkServer.activeHost, Is.True, "Should be in host mode");

            // Create objects with cross-references
            CreateNetworkedAndSpawn(out _, out _, out CrossRefBehaviour comp1);
            CreateNetworkedAndSpawn(out _, out NetworkIdentity identity2, out CrossRefBehaviour comp2);
            CreateNetworkedAndSpawn(out _, out NetworkIdentity identity3, out CrossRefBehaviour comp3);

            // In host mode, there's only one set of objects (not separate server/client)
            // Set circular references
            comp1.target = identity2;
            comp2.target = identity3;
            comp3.target = comp1.netIdentity;

            // In host mode, hooks should fire IMMEDIATELY from setters, NOT deferred
            Assert.That(comp1.callCount, Is.EqualTo(1),
                "Host mode:  Hook should fire from setter immediately");
            Assert.That(comp2.callCount, Is.EqualTo(1),
                "Host mode: Hook should fire from setter immediately");
            Assert.That(comp3.callCount, Is.EqualTo(1),
                "Host mode:  Hook should fire from setter immediately");

            // All targets should be accessible (host mode, everything is local)
            Assert.That(comp1.targetWasInSpawnedWhenHookFired, Is.True,
                "Host mode:  Targets always accessible");
            Assert.That(comp2.targetWasInSpawnedWhenHookFired, Is.True,
                "Host mode: Targets always accessible");
            Assert.That(comp3.targetWasInSpawnedWhenHookFired, Is.True,
                "Host mode: Targets always accessible");

            // Verify no deferred hooks queued (host mode doesn't defer)
            Assert.That(comp1.deferredSyncVarHooks.Count, Is.EqualTo(0),
                "Host mode should not defer hooks");
            Assert.That(comp2.deferredSyncVarHooks.Count, Is.EqualTo(0),
                "Host mode should not defer hooks");
            Assert.That(comp3.deferredSyncVarHooks.Count, Is.EqualTo(0),
                "Host mode should not defer hooks");
        }

        [Test]
        public void MultipleHooksOnSameObject_FireInOrder()
        {
            NetworkServer.Listen(10);
            ConnectClientBlockingAuthenticatedAndReady(out _);

            CreateNetworkedAndSpawn(
                out _, out _, out MultiHookBehaviour serverComp,
                out _, out _, out MultiHookBehaviour clientComp);

            // Set all SyncVars
            serverComp.value1 = 10;
            serverComp.value2 = 20;
            serverComp.value3 = 30;

            ProcessMessages();

            // Verify all hooks fired in declaration order
            Assert.That(clientComp.callOrder.Count, Is.EqualTo(3));
            Assert.That(clientComp.callOrder[0], Is.EqualTo("value1"));
            Assert.That(clientComp.callOrder[1], Is.EqualTo("value2"));
            Assert.That(clientComp.callOrder[2], Is.EqualTo("value3"));
        }

        [Test]
        public void NetworkBehaviour_CrossReferences_HooksCanAccessTargets()
        {
            NetworkServer.Listen(10);
            ConnectClientBlockingAuthenticatedAndReady(out _);

            // Create objects with NetworkBehaviour SyncVar cross-references
            CreateNetworkedAndSpawn(
                out _, out _, out NBCrossRefBehaviour serverComp1,
                out _, out _, out NBCrossRefBehaviour clientComp1);

            CreateNetworkedAndSpawn(
                out _, out _, out NBCrossRefBehaviour serverComp2,
                out _, out _, out NBCrossRefBehaviour clientComp2);

            CreateNetworkedAndSpawn(
                out _, out _, out NBCrossRefBehaviour serverComp3,
                out _, out _, out NBCrossRefBehaviour clientComp3);

            // Set circular NetworkBehaviour component references
            serverComp1.targetBehaviour = serverComp2;
            serverComp2.targetBehaviour = serverComp3;
            serverComp3.targetBehaviour = serverComp1;

            ProcessMessages();

            // Verify hooks fired
            Assert.That(clientComp1.callCount, Is.EqualTo(1));
            Assert.That(clientComp2.callCount, Is.EqualTo(1));
            Assert.That(clientComp3.callCount, Is.EqualTo(1));

            // Critical: NetworkBehaviour targets were accessible during hooks
            Assert.That(clientComp1.targetWasInSpawnedWhenHookFired, Is.True,
                "NetworkBehaviour target should be spawned when hook fires");
            Assert.That(clientComp2.targetWasInSpawnedWhenHookFired, Is.True,
                "NetworkBehaviour target should be spawned when hook fires");
            Assert.That(clientComp3.targetWasInSpawnedWhenHookFired, Is.True,
                "NetworkBehaviour target should be spawned when hook fires");

            // Verify references are correct
            Assert.That(clientComp1.targetBehaviour, Is.EqualTo(clientComp2));
            Assert.That(clientComp2.targetBehaviour, Is.EqualTo(clientComp3));
            Assert.That(clientComp3.targetBehaviour, Is.EqualTo(clientComp1));
        }

        [Test]
        public void UpdateAfterSpawn_HooksFireImmediately()
        {
            NetworkServer.Listen(10);
            ConnectClientBlockingAuthenticatedAndReady(out _);

            // Create and spawn objects
            CreateNetworkedAndSpawn(
                out _, out _, out CrossRefBehaviour serverComp1,
                out _, out _, out CrossRefBehaviour clientComp1);

            CreateNetworkedAndSpawn(
                out _, out NetworkIdentity serverIdentity2,
                out _, out NetworkIdentity clientIdentity2);

            // Initial spawn is complete
            Assert.That(NetworkClient.isSpawnFinished, Is.True);

            // Set target after spawn
            serverComp1.target = serverIdentity2;
            ProcessMessages();

            // Hook should fire immediately (not deferred)
            Assert.That(clientComp1.callCount, Is.EqualTo(1));
            Assert.That(clientComp1.target, Is.EqualTo(clientIdentity2));
            Assert.That(clientComp1.targetWasInSpawnedWhenHookFired, Is.True);

            // Change target
            serverComp1.target = null;
            ProcessMessages();

            // Hook fires again immediately
            Assert.That(clientComp1.callCount, Is.EqualTo(2));
            Assert.That(clientComp1.target, Is.Null);
        }
    }

    public class CrossRefBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnTargetChanged))]
        public NetworkIdentity target;

        public int callCount;
        public bool targetWasInSpawnedWhenHookFired;

        void OnTargetChanged(NetworkIdentity oldValue, NetworkIdentity newValue)
        {
            callCount++;

            if (newValue != null)
                targetWasInSpawnedWhenHookFired = NetworkClient.spawned.ContainsKey(newValue.netId);
        }
    }

    public class GameObjectCrossRefBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnTargetChanged))]
        public GameObject targetGO;

        public int callCount;
        public bool targetWasInSpawnedWhenHookFired;

        void OnTargetChanged(GameObject oldValue, GameObject newValue)
        {
            callCount++;

            if (newValue != null)
            {
                NetworkIdentity targetIdentity = newValue.GetComponent<NetworkIdentity>();
                if (targetIdentity != null)
                    targetWasInSpawnedWhenHookFired = NetworkClient.spawned.ContainsKey(targetIdentity.netId);
            }
        }
    }

    public class MultiHookBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnValue1Changed))] public int value1;
        [SyncVar(hook = nameof(OnValue2Changed))] public int value2;
        [SyncVar(hook = nameof(OnValue3Changed))] public int value3;

        public System.Collections.Generic.List<string> callOrder = new System.Collections.Generic.List<string>();

        void OnValue1Changed(int old, int newVal) => callOrder.Add("value1");
        void OnValue2Changed(int old, int newVal) => callOrder.Add("value2");
        void OnValue3Changed(int old, int newVal) => callOrder.Add("value3");
    }

    public class NBCrossRefBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnTargetChanged))]
        public NBCrossRefBehaviour targetBehaviour;

        public int callCount;
        public bool targetWasInSpawnedWhenHookFired;

        void OnTargetChanged(NBCrossRefBehaviour oldValue, NBCrossRefBehaviour newValue)
        {
            callCount++;

            if (newValue != null)
                targetWasInSpawnedWhenHookFired = NetworkClient.spawned.ContainsKey(newValue.netId);
        }
    }
}
