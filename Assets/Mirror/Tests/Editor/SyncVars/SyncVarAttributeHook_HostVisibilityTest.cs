using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.SyncVars
{
    struct HostHookStructValue
    {
        public int value;
    }

    class HostVisibilityHookBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnValueChanged))]
        public int value = 42;

        public readonly List<(int oldValue, int newValue)> hookValues = new List<(int oldValue, int newValue)>();

        void OnValueChanged(int oldValue, int newValue) => hookValues.Add((oldValue, newValue));
    }

    class HostVisibilityStructHookBehaviour : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnValueChanged))]
        public HostHookStructValue value = new HostHookStructValue { value = 5 };

        public readonly List<(HostHookStructValue oldValue, HostHookStructValue newValue)> hookValues = new List<(HostHookStructValue oldValue, HostHookStructValue newValue)>();

        void OnValueChanged(HostHookStructValue oldValue, HostHookStructValue newValue) => hookValues.Add((oldValue, newValue));
    }

    public class SyncVarAttributeHook_HostVisibilityTest : MirrorTest
    {
        DistanceInterestManagement aoi;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            aoi = holder.AddComponent<DistanceInterestManagement>();
            aoi.visRange = 10;
            NetworkServer.aoi = aoi;
            NetworkClient.aoi = aoi;

            NetworkServer.Listen(1);
            ConnectHostClientBlockingAuthenticatedAndReady();
        }

        [TearDown]
        public override void TearDown()
        {
            NetworkClient.aoi = null;
            NetworkServer.aoi = null;
            base.TearDown();
        }

        void AddLocalPlayer(Vector3 position)
        {
            CreateNetworked(out GameObject player, out NetworkIdentity identity);
            player.transform.position = position;
            NetworkServer.AddPlayerForConnection(NetworkServer.localConnection, player);
            ProcessMessages();
            Assert.That(NetworkClient.localPlayer, Is.EqualTo(identity));
        }

        void RebuildLocalObserver(NetworkIdentity identity, Vector3 localPlayerPosition)
        {
            NetworkClient.localPlayer.transform.position = localPlayerPosition;
            NetworkServer.RebuildObservers(identity, false);
            ProcessMessages();
        }

        void AssertObserved(NetworkIdentity identity, bool expected)
        {
            Assert.That(NetworkServer.localConnection.observing.Contains(identity), Is.EqualTo(expected));
            Assert.That(identity.observers.ContainsKey(NetworkServer.localConnection.connectionId), Is.EqualTo(expected));
        }

        [Test]
        public void Hook_UsesDeclarationInitializerBaselineUntilObserved()
        {
            CreateNetworked(out GameObject go, out NetworkIdentity identity, out HostVisibilityHookBehaviour behaviour);
            go.transform.position = Vector3.zero;
            NetworkServer.Spawn(go);
            ProcessMessages();

            behaviour.value = 50;
            behaviour.value = 100;

            Assert.That(behaviour.hookValues, Is.Empty);

            AddLocalPlayer(Vector3.right * (aoi.visRange + 1));
            Assert.That(behaviour.hookValues, Is.Empty);

            NetworkClient.localPlayer.transform.position = Vector3.zero;
            NetworkServer.RebuildObservers(identity, false);
            ProcessMessages();

            Assert.That(behaviour.hookValues.Count, Is.EqualTo(1));
            Assert.That(behaviour.hookValues[0].oldValue, Is.EqualTo(42));
            Assert.That(behaviour.hookValues[0].newValue, Is.EqualTo(100));
        }

        [Test]
        public void Hook_DoesNotFireWhenStateReturnsToBaselineBeforeObserved()
        {
            CreateNetworked(out GameObject go, out NetworkIdentity identity, out HostVisibilityHookBehaviour behaviour);
            go.transform.position = Vector3.zero;
            NetworkServer.Spawn(go);
            ProcessMessages();

            behaviour.value = 100;
            behaviour.value = 42;

            AddLocalPlayer(Vector3.zero);
            NetworkServer.RebuildObservers(identity, false);
            ProcessMessages();

            Assert.That(behaviour.hookValues, Is.Empty);
        }

        [Test]
        public void Hook_SupportsStructSyncVars()
        {
            CreateNetworked(out GameObject go, out NetworkIdentity identity, out HostVisibilityStructHookBehaviour behaviour);
            go.transform.position = Vector3.zero;
            NetworkServer.Spawn(go);
            ProcessMessages();

            behaviour.value = new HostHookStructValue { value = 9 };

            AddLocalPlayer(Vector3.right * (aoi.visRange + 1));
            Assert.That(behaviour.hookValues, Is.Empty);

            NetworkClient.localPlayer.transform.position = Vector3.zero;
            NetworkServer.RebuildObservers(identity, false);
            ProcessMessages();

            Assert.That(behaviour.hookValues.Count, Is.EqualTo(1));
            Assert.That(behaviour.hookValues[0].oldValue.value, Is.EqualTo(5));
            Assert.That(behaviour.hookValues[0].newValue.value, Is.EqualTo(9));
        }

        [Test]
        public void Hook_UsesLastObservedValueAsBaselineAfterLeavingAoi()
        {
            CreateNetworked(out GameObject go, out NetworkIdentity identity, out HostVisibilityHookBehaviour behaviour);
            go.transform.position = Vector3.zero;
            NetworkServer.Spawn(go);
            ProcessMessages();

            behaviour.value = 100;

            AddLocalPlayer(Vector3.right * (aoi.visRange + 1));
            AssertObserved(identity, false);

            RebuildLocalObserver(identity, Vector3.zero);
            AssertObserved(identity, true);
            Assert.That(behaviour.hookValues, Is.EqualTo(new[] { (42, 100) }));

            RebuildLocalObserver(identity, Vector3.right * (aoi.visRange + 1));
            AssertObserved(identity, false);

            behaviour.value = 150;
            behaviour.value = 200;

            Assert.That(behaviour.hookValues, Is.EqualTo(new[] { (42, 100) }));

            RebuildLocalObserver(identity, Vector3.zero);
            AssertObserved(identity, true);
            Assert.That(behaviour.hookValues, Is.EqualTo(new[] { (42, 100), (100, 200) }));
        }
    }
}
