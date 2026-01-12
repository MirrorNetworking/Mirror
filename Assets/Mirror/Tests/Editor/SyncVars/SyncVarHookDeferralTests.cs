using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.SyncVars
{
    // Test components for cross-reference testing
    class CrossRefBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnTargetChanged))]
        public NetworkIdentity target;

        public bool targetWasInSpawnedWhenHookFired = false;
        public int hookCallCount = 0;

        void OnTargetChanged(NetworkIdentity oldValue, NetworkIdentity newValue)
        {
            hookCallCount++;
            
            // This is THE test - verify target is in spawned when hook fires
            if (newValue != null)
            {
                targetWasInSpawnedWhenHookFired = NetworkClient.spawned.ContainsKey(newValue.netId);
            }
        }
    }

    // Test component for GameObject SyncVar cross-references
    class GameObjectCrossRefBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnTargetChanged))]
        public GameObject target;

        public bool targetWasInSpawnedWhenHookFired = false;

        void OnTargetChanged(GameObject oldValue, GameObject newValue)
        {
            if (newValue != null)
            {
                NetworkIdentity identity = newValue.GetComponent<NetworkIdentity>();
                if (identity != null)
                {
                    targetWasInSpawnedWhenHookFired = NetworkClient.spawned.ContainsKey(identity.netId);
                }
            }
        }
    }

    // Test component for NetworkBehaviour SyncVar cross-references
    class NetworkBehaviourCrossRefBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnTargetChanged))]
        public NetworkBehaviourCrossRefBehaviour target;

        public bool targetWasInSpawnedWhenHookFired = false;

        void OnTargetChanged(NetworkBehaviourCrossRefBehaviour oldValue, NetworkBehaviourCrossRefBehaviour newValue)
        {
            if (newValue != null)
            {
                targetWasInSpawnedWhenHookFired = NetworkClient.spawned.ContainsKey(newValue.netIdentity.netId);
            }
        }
    }

    // Test component to verify hook declaration order
    class MultipleHookBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnFirstChanged))]
        public int first;

        [SyncVar(hook = nameof(OnSecondChanged))]
        public int second;

        [SyncVar(hook = nameof(OnThirdChanged))]
        public int third;

        public List<string> hookCallOrder = new List<string>();

        void OnFirstChanged(int oldValue, int newValue)
        {
            hookCallOrder.Add("first");
        }

        void OnSecondChanged(int oldValue, int newValue)
        {
            hookCallOrder.Add("second");
        }

        void OnThirdChanged(int oldValue, int newValue)
        {
            hookCallOrder.Add("third");
        }
    }

    // Test component for post-spawn updates
    class PostSpawnUpdateBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnValueChanged))]
        public int value;

        public int hookCallCount = 0;

        void OnValueChanged(int oldValue, int newValue)
        {
            hookCallCount++;
        }
    }

    public class SyncVarHookDeferralTests : MirrorTest
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            
            // Start server and connect client - non-host mode to test deferral
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out _);
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        [Test]
        public void CrossReferences_HooksCanAccessAllTargets()
        {
            // This is THE test that validates the fix!
            // Create two objects that reference each other
            CreateNetworked(out GameObject serverGO1, out NetworkIdentity serverIdentity1, out CrossRefBehaviour serverComp1);
            CreateNetworked(out GameObject clientGO1, out NetworkIdentity clientIdentity1, out CrossRefBehaviour clientComp1);

            CreateNetworked(out GameObject serverGO2, out NetworkIdentity serverIdentity2, out CrossRefBehaviour serverComp2);
            CreateNetworked(out GameObject clientGO2, out NetworkIdentity clientIdentity2, out CrossRefBehaviour clientComp2);

            // Set up cross-references on server
            serverComp1.target = serverIdentity2;
            serverComp2.target = serverIdentity1;

            // Give both scene ids and register on client
            clientIdentity1.sceneId = serverIdentity1.sceneId = (ulong)serverGO1.GetHashCode();
            clientIdentity2.sceneId = serverIdentity2.sceneId = (ulong)serverGO2.GetHashCode();
            NetworkClient.spawnableObjects[clientIdentity1.sceneId] = clientIdentity1;
            NetworkClient.spawnableObjects[clientIdentity2.sceneId] = clientIdentity2;

            // Disconnect and reconnect to simulate initial spawn batch
            NetworkClient.Disconnect();
            NetworkServer.RemoveConnection(NetworkServer.connections[0]);
            UpdateTransport();

            // Reconnect
            ConnectClientBlockingAuthenticatedAndReady(out _);

            // Spawn both objects - they will be in the same spawn batch
            NetworkServer.Spawn(serverGO1);
            NetworkServer.Spawn(serverGO2);

            // Process messages - this triggers initial spawn with deferral
            ProcessMessages();

            // THE CRITICAL ASSERTION - this should PASS with deferral, FAIL without it
            Assert.That(clientComp1.targetWasInSpawnedWhenHookFired, Is.True,
                "Target should be in spawned when hook fires - THIS IS THE FIX!");
            Assert.That(clientComp2.targetWasInSpawnedWhenHookFired, Is.True,
                "Target should be in spawned when hook fires - THIS IS THE FIX!");

            // Verify hooks were called
            Assert.That(clientComp1.hookCallCount, Is.EqualTo(1));
            Assert.That(clientComp2.hookCallCount, Is.EqualTo(1));

            // Verify final values are correct
            Assert.That(clientComp1.target, Is.EqualTo(clientIdentity2));
            Assert.That(clientComp2.target, Is.EqualTo(clientIdentity1));
        }

        [Test]
        public void GameObject_CrossReferences_HooksCanAccessTargets()
        {
            // Test GameObject SyncVar variant
            CreateNetworked(out GameObject serverGO1, out NetworkIdentity serverIdentity1, out GameObjectCrossRefBehaviour serverComp1);
            CreateNetworked(out GameObject clientGO1, out NetworkIdentity clientIdentity1, out GameObjectCrossRefBehaviour clientComp1);

            CreateNetworked(out GameObject serverGO2, out NetworkIdentity serverIdentity2);
            CreateNetworked(out GameObject clientGO2, out NetworkIdentity clientIdentity2);

            // Set up cross-reference on server
            serverComp1.target = serverGO2;

            // Give both scene ids and register on client
            clientIdentity1.sceneId = serverIdentity1.sceneId = (ulong)serverGO1.GetHashCode();
            clientIdentity2.sceneId = serverIdentity2.sceneId = (ulong)serverGO2.GetHashCode();
            NetworkClient.spawnableObjects[clientIdentity1.sceneId] = clientIdentity1;
            NetworkClient.spawnableObjects[clientIdentity2.sceneId] = clientIdentity2;

            // Disconnect and reconnect to simulate initial spawn batch
            NetworkClient.Disconnect();
            NetworkServer.RemoveConnection(NetworkServer.connections[0]);
            UpdateTransport();

            // Reconnect
            ConnectClientBlockingAuthenticatedAndReady(out _);

            // Spawn both objects
            NetworkServer.Spawn(serverGO1);
            NetworkServer.Spawn(serverGO2);

            // Process messages
            ProcessMessages();

            // Verify target was in spawned when hook fired
            Assert.That(clientComp1.targetWasInSpawnedWhenHookFired, Is.True,
                "GameObject target should be in spawned when hook fires");

            // Verify final value is correct
            Assert.That(clientComp1.target, Is.EqualTo(clientGO2));
        }

        [Test]
        public void NetworkBehaviour_CrossReferences_HooksCanAccessTargets()
        {
            // Test NetworkBehaviour SyncVar variant
            CreateNetworked(out GameObject serverGO1, out NetworkIdentity serverIdentity1, out NetworkBehaviourCrossRefBehaviour serverComp1);
            CreateNetworked(out GameObject clientGO1, out NetworkIdentity clientIdentity1, out NetworkBehaviourCrossRefBehaviour clientComp1);

            CreateNetworked(out GameObject serverGO2, out NetworkIdentity serverIdentity2, out NetworkBehaviourCrossRefBehaviour serverComp2);
            CreateNetworked(out GameObject clientGO2, out NetworkIdentity clientIdentity2, out NetworkBehaviourCrossRefBehaviour clientComp2);

            // Set up cross-reference on server
            serverComp1.target = serverComp2;

            // Give both scene ids and register on client
            clientIdentity1.sceneId = serverIdentity1.sceneId = (ulong)serverGO1.GetHashCode();
            clientIdentity2.sceneId = serverIdentity2.sceneId = (ulong)serverGO2.GetHashCode();
            NetworkClient.spawnableObjects[clientIdentity1.sceneId] = clientIdentity1;
            NetworkClient.spawnableObjects[clientIdentity2.sceneId] = clientIdentity2;

            // Disconnect and reconnect to simulate initial spawn batch
            NetworkClient.Disconnect();
            NetworkServer.RemoveConnection(NetworkServer.connections[0]);
            UpdateTransport();

            // Reconnect
            ConnectClientBlockingAuthenticatedAndReady(out _);

            // Spawn both objects
            NetworkServer.Spawn(serverGO1);
            NetworkServer.Spawn(serverGO2);

            // Process messages
            ProcessMessages();

            // Verify target was in spawned when hook fired
            Assert.That(clientComp1.targetWasInSpawnedWhenHookFired, Is.True,
                "NetworkBehaviour target should be in spawned when hook fires");

            // Verify final value is correct
            Assert.That(clientComp1.target, Is.EqualTo(clientComp2));
        }

        [Test]
        public void HostMode_CrossReferences_HooksFireFromSetter_NotDeferred()
        {
            // Host mode should NOT defer hooks - they should fire immediately from setter
            NetworkClient.Disconnect();
            NetworkServer.RemoveConnection(NetworkServer.connections[0]);
            UpdateTransport();

            // Connect in host mode
            ConnectHostClientBlockingAuthenticatedAndReady();

            // Create and spawn objects in host mode
            CreateNetworkedAndSpawn(
                out GameObject go1, out NetworkIdentity identity1, out CrossRefBehaviour comp1);
            CreateNetworkedAndSpawn(
                out GameObject go2, out NetworkIdentity identity2, out CrossRefBehaviour comp2);

            // Set cross-reference on host - hook should fire immediately
            comp1.target = identity2;
            
            // In host mode, hooks fire immediately, so hook was called during setter
            Assert.That(comp1.hookCallCount, Is.EqualTo(1),
                "In host mode, hook should fire immediately from setter");
        }

        [Test]
        public void MultipleHooksOnSameObject_FireInOrder()
        {
            // Verify hooks fire in declaration order
            CreateNetworked(out GameObject serverGO, out NetworkIdentity serverIdentity, out MultipleHookBehaviour serverComp);
            CreateNetworked(out GameObject clientGO, out NetworkIdentity clientIdentity, out MultipleHookBehaviour clientComp);

            // Set values on server
            serverComp.first = 1;
            serverComp.second = 2;
            serverComp.third = 3;

            // Give scene id and register on client
            clientIdentity.sceneId = serverIdentity.sceneId = (ulong)serverGO.GetHashCode();
            NetworkClient.spawnableObjects[clientIdentity.sceneId] = clientIdentity;

            // Disconnect and reconnect to simulate initial spawn batch
            NetworkClient.Disconnect();
            NetworkServer.RemoveConnection(NetworkServer.connections[0]);
            UpdateTransport();

            // Reconnect
            ConnectClientBlockingAuthenticatedAndReady(out _);

            // Spawn object
            NetworkServer.Spawn(serverGO);

            // Process messages
            ProcessMessages();

            // Verify hooks fired in declaration order
            Assert.That(clientComp.hookCallOrder.Count, Is.EqualTo(3));
            Assert.That(clientComp.hookCallOrder[0], Is.EqualTo("first"));
            Assert.That(clientComp.hookCallOrder[1], Is.EqualTo("second"));
            Assert.That(clientComp.hookCallOrder[2], Is.EqualTo("third"));
        }

        [Test]
        public void UpdateAfterSpawn_HooksFireImmediately()
        {
            // After initial spawn completes, hooks should fire immediately
            CreateNetworkedAndSpawn(
                out GameObject serverGO, out NetworkIdentity serverIdentity, out PostSpawnUpdateBehaviour serverComp,
                out GameObject clientGO, out NetworkIdentity clientIdentity, out PostSpawnUpdateBehaviour clientComp);

            // Initial spawn completes, hook was called (or not if value was same)
            int initialCallCount = clientComp.hookCallCount;

            // Update value after spawn - hook should fire immediately
            serverComp.value = 42;
            ProcessMessages();

            // Hook should have been called immediately for post-spawn update
            Assert.That(clientComp.hookCallCount, Is.EqualTo(initialCallCount + 1),
                "Post-spawn updates should fire hooks immediately");
            Assert.That(clientComp.value, Is.EqualTo(42));
        }
    }
}
