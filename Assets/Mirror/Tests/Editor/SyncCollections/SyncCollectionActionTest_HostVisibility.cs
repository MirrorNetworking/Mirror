using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.SyncCollections
{
    class HostVisibilitySyncListBehaviour : NetworkBehaviour
    {
        public readonly SyncList<string> list = new SyncList<string>();
        public readonly List<string> actions = new List<string>();

        public void Register() => list.OnAdd += index => actions.Add($"Add:{list[index]}");
    }

    class HostVisibilitySyncDictionaryBehaviour : NetworkBehaviour
    {
        public readonly SyncDictionary<string, string> dictionary = new SyncDictionary<string, string>();
        public readonly List<string> actions = new List<string>();

        public void Register() => dictionary.OnAdd += key => actions.Add($"Add:{key}:{dictionary[key]}");
    }

    class HostVisibilitySyncSetBehaviour : NetworkBehaviour
    {
        public readonly SyncHashSet<string> set = new SyncHashSet<string>();
        public readonly List<string> actions = new List<string>();

        public void Register() => set.OnAdd += item => actions.Add($"Add:{item}");
    }

    public class SyncCollectionActionTest_HostVisibility : MirrorTest
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
            CreateNetworked(out GameObject player, out _);
            player.transform.position = position;
            NetworkServer.AddPlayerForConnection(NetworkServer.localConnection, player);
            ProcessMessages();
        }

        void AddLocalPlayerWithoutProcessing(Vector3 position)
        {
            CreateNetworked(out GameObject player, out _);
            player.transform.position = position;
            NetworkServer.AddPlayerForConnection(NetworkServer.localConnection, player);
        }

        void RebuildLocalObserver(NetworkIdentity identity, Vector3 localPlayerPosition)
        {
            NetworkClient.localPlayer.transform.position = localPlayerPosition;
            NetworkServer.RebuildObservers(identity, false);
            ProcessMessages();
        }

        static void InvokeHostVisibilityDeferredCallbacks(NetworkIdentity identity)
        {
            identity.hostInitialSpawn = true;
            try
            {
                foreach (NetworkBehaviour component in identity.NetworkBehaviours)
                    component.InvokeHostVisibilityDeferredCallbacks();
            }
            finally
            {
                identity.hostInitialSpawn = false;
            }
        }

        void AssertObserved(NetworkIdentity identity, bool expected)
        {
            Assert.That(NetworkServer.localConnection.observing.Contains(identity), Is.EqualTo(expected));
            Assert.That(identity.observers.ContainsKey(NetworkServer.localConnection.connectionId), Is.EqualTo(expected));
        }

        static int GetDeltaChangeCount(SyncObject syncObject)
        {
            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                syncObject.OnSerializeDelta(writer);
                using (NetworkReaderPooled reader = NetworkReaderPool.Get(writer.ToArraySegment()))
                    return (int)reader.ReadUInt();
            }
        }

        [Test]
        public void SyncList_ActionsDeferUntilObserved()
        {
            CreateNetworked(out GameObject go, out NetworkIdentity identity, out HostVisibilitySyncListBehaviour behaviour);
            go.transform.position = Vector3.zero;
            behaviour.Register();
            NetworkServer.Spawn(go);
            ProcessMessages();

            behaviour.list.Add("first");
            behaviour.list.Add("second");
            Assert.That(behaviour.actions, Is.Empty);

            AddLocalPlayer(Vector3.right * (aoi.visRange + 1));
            Assert.That(behaviour.actions, Is.Empty);

            NetworkClient.localPlayer.transform.position = Vector3.zero;
            NetworkServer.RebuildObservers(identity, false);
            ProcessMessages();

            Assert.That(behaviour.actions, Is.EqualTo(new[] { "Add:first", "Add:second" }));
        }

        [Test]
        public void SyncDictionary_ActionsDeferUntilObserved()
        {
            CreateNetworked(out GameObject go, out NetworkIdentity identity, out HostVisibilitySyncDictionaryBehaviour behaviour);
            go.transform.position = Vector3.zero;
            behaviour.Register();
            NetworkServer.Spawn(go);
            ProcessMessages();

            behaviour.dictionary.Add("key1", "first");
            behaviour.dictionary.Add("key2", "second");
            Assert.That(behaviour.actions, Is.Empty);

            AddLocalPlayer(Vector3.right * (aoi.visRange + 1));
            Assert.That(behaviour.actions, Is.Empty);

            NetworkClient.localPlayer.transform.position = Vector3.zero;
            NetworkServer.RebuildObservers(identity, false);
            ProcessMessages();

            Assert.That(behaviour.actions, Is.EqualTo(new[] { "Add:key1:first", "Add:key2:second" }));
        }

        [Test]
        public void SyncSet_ActionsDeferUntilObserved()
        {
            CreateNetworked(out GameObject go, out NetworkIdentity identity, out HostVisibilitySyncSetBehaviour behaviour);
            go.transform.position = Vector3.zero;
            behaviour.Register();
            NetworkServer.Spawn(go);
            ProcessMessages();

            behaviour.set.Add("first");
            behaviour.set.Add("second");
            Assert.That(behaviour.actions, Is.Empty);

            AddLocalPlayer(Vector3.right * (aoi.visRange + 1));
            Assert.That(behaviour.actions, Is.Empty);

            NetworkClient.localPlayer.transform.position = Vector3.zero;
            NetworkServer.RebuildObservers(identity, false);
            ProcessMessages();

            Assert.That(behaviour.actions, Is.EqualTo(new[] { "Add:first", "Add:second" }));
        }

        [Test]
        public void SyncList_ActionsDeferAgainAfterLeavingAoi()
        {
            CreateNetworked(out GameObject go, out NetworkIdentity identity, out HostVisibilitySyncListBehaviour behaviour);
            go.transform.position = Vector3.zero;
            behaviour.Register();
            NetworkServer.Spawn(go);
            ProcessMessages();

            behaviour.list.Add("first");

            AddLocalPlayer(Vector3.right * (aoi.visRange + 1));
            AssertObserved(identity, false);

            RebuildLocalObserver(identity, Vector3.zero);
            AssertObserved(identity, true);
            Assert.That(behaviour.actions, Is.EqualTo(new[] { "Add:first" }));

            RebuildLocalObserver(identity, Vector3.right * (aoi.visRange + 1));
            AssertObserved(identity, false);
            behaviour.actions.Clear();

            behaviour.list.Add("second");
            Assert.That(behaviour.actions, Is.Empty);

            RebuildLocalObserver(identity, Vector3.zero);
            AssertObserved(identity, true);
            Assert.That(behaviour.actions, Is.EqualTo(new[] { "Add:first", "Add:second" }));
        }

        [Test]
        public void SyncList_HostVisibilityReplayPreservesPendingRemoteChanges()
        {
            CreateNetworked(out GameObject go, out NetworkIdentity identity, out HostVisibilitySyncListBehaviour behaviour);
            go.transform.position = Vector3.zero;
            behaviour.Register();
            NetworkServer.Spawn(go);
            ProcessMessages();

            NetworkConnectionToClient remoteConnection = new NetworkConnectionToClient(42);
            NetworkServer.connections[remoteConnection.connectionId] = remoteConnection;
            identity.AddObserver(remoteConnection);

            behaviour.list.Add("first");
            Assert.That(GetDeltaChangeCount(behaviour.list), Is.EqualTo(1));

            AddLocalPlayerWithoutProcessing(Vector3.zero);
            AssertObserved(identity, true);
            InvokeHostVisibilityDeferredCallbacks(identity);
            Assert.That(behaviour.actions, Is.EqualTo(new[] { "Add:first" }));
            Assert.That(GetDeltaChangeCount(behaviour.list), Is.EqualTo(1));
        }

        [Test]
        public void SyncDictionary_ActionsDeferAgainAfterLeavingAoi()
        {
            CreateNetworked(out GameObject go, out NetworkIdentity identity, out HostVisibilitySyncDictionaryBehaviour behaviour);
            go.transform.position = Vector3.zero;
            behaviour.Register();
            NetworkServer.Spawn(go);
            ProcessMessages();

            behaviour.dictionary.Add("key1", "first");

            AddLocalPlayer(Vector3.right * (aoi.visRange + 1));
            AssertObserved(identity, false);

            RebuildLocalObserver(identity, Vector3.zero);
            AssertObserved(identity, true);
            Assert.That(behaviour.actions, Is.EqualTo(new[] { "Add:key1:first" }));

            RebuildLocalObserver(identity, Vector3.right * (aoi.visRange + 1));
            AssertObserved(identity, false);
            behaviour.actions.Clear();

            behaviour.dictionary.Add("key2", "second");
            Assert.That(behaviour.actions, Is.Empty);

            RebuildLocalObserver(identity, Vector3.zero);
            AssertObserved(identity, true);
            Assert.That(behaviour.actions, Is.EqualTo(new[] { "Add:key1:first", "Add:key2:second" }));
        }

        [Test]
        public void SyncDictionary_HostVisibilityReplayPreservesPendingRemoteChanges()
        {
            CreateNetworked(out GameObject go, out NetworkIdentity identity, out HostVisibilitySyncDictionaryBehaviour behaviour);
            go.transform.position = Vector3.zero;
            behaviour.Register();
            NetworkServer.Spawn(go);
            ProcessMessages();

            NetworkConnectionToClient remoteConnection = new NetworkConnectionToClient(42);
            NetworkServer.connections[remoteConnection.connectionId] = remoteConnection;
            identity.AddObserver(remoteConnection);

            behaviour.dictionary.Add("key1", "first");
            Assert.That(GetDeltaChangeCount(behaviour.dictionary), Is.EqualTo(1));

            AddLocalPlayerWithoutProcessing(Vector3.zero);
            AssertObserved(identity, true);
            InvokeHostVisibilityDeferredCallbacks(identity);
            Assert.That(behaviour.actions, Is.EqualTo(new[] { "Add:key1:first" }));
            Assert.That(GetDeltaChangeCount(behaviour.dictionary), Is.EqualTo(1));
        }

        [Test]
        public void SyncSet_ActionsDeferAgainAfterLeavingAoi()
        {
            CreateNetworked(out GameObject go, out NetworkIdentity identity, out HostVisibilitySyncSetBehaviour behaviour);
            go.transform.position = Vector3.zero;
            behaviour.Register();
            NetworkServer.Spawn(go);
            ProcessMessages();

            behaviour.set.Add("first");

            AddLocalPlayer(Vector3.right * (aoi.visRange + 1));
            AssertObserved(identity, false);

            RebuildLocalObserver(identity, Vector3.zero);
            AssertObserved(identity, true);
            Assert.That(behaviour.actions, Is.EqualTo(new[] { "Add:first" }));

            RebuildLocalObserver(identity, Vector3.right * (aoi.visRange + 1));
            AssertObserved(identity, false);
            behaviour.actions.Clear();

            behaviour.set.Add("second");
            Assert.That(behaviour.actions, Is.Empty);

            RebuildLocalObserver(identity, Vector3.zero);
            AssertObserved(identity, true);
            Assert.That(behaviour.actions, Is.EqualTo(new[] { "Add:first", "Add:second" }));
        }

        [Test]
        public void SyncSet_HostVisibilityReplayPreservesPendingRemoteChanges()
        {
            CreateNetworked(out GameObject go, out NetworkIdentity identity, out HostVisibilitySyncSetBehaviour behaviour);
            go.transform.position = Vector3.zero;
            behaviour.Register();
            NetworkServer.Spawn(go);
            ProcessMessages();

            NetworkConnectionToClient remoteConnection = new NetworkConnectionToClient(42);
            NetworkServer.connections[remoteConnection.connectionId] = remoteConnection;
            identity.AddObserver(remoteConnection);

            behaviour.set.Add("first");
            Assert.That(GetDeltaChangeCount(behaviour.set), Is.EqualTo(1));

            AddLocalPlayerWithoutProcessing(Vector3.zero);
            AssertObserved(identity, true);
            InvokeHostVisibilityDeferredCallbacks(identity);
            Assert.That(behaviour.actions, Is.EqualTo(new[] { "Add:first" }));
            Assert.That(GetDeltaChangeCount(behaviour.set), Is.EqualTo(1));
        }
    }
}
