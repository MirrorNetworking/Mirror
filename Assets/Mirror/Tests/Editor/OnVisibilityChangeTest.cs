using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class FakeVisibility : NetworkVisibility
    {
        NetworkConnection visibleConnection;

        public void SetConnectionAsVisible(NetworkConnection conn)
        {
            visibleConnection = conn;
            netIdentity.RebuildObservers(false);
        }

        public override bool OnCheckObserver(NetworkConnection conn)
        {
            return visibleConnection == conn;
        }

        public override void OnRebuildObservers(HashSet<NetworkConnection> observers, bool initialize)
        {
            if (visibleConnection != null)
                observers.Add(visibleConnection);
        }
    }
    public class OnShowBehaviour : NetworkBehaviour
    {
        public event Action<NetworkConnection> onShown;
        public event Action<NetworkConnection> onHide;

        public override void OnShownForConnection(NetworkConnection conn)
        {
            onShown?.Invoke(conn);
        }
        public override void OnHiddenForConnection(NetworkConnection conn)
        {
            onHide?.Invoke(conn);
        }
    }
    [TestFixture]
    public class OnVisibilityChangeTest
    {
        private GameObject go;
        private FakeVisibility visibility;
        private OnShowBehaviour behaviour;
        private FakeNetworkConnection fakeConnection;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject();
            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();
            visibility = go.AddComponent<FakeVisibility>();
            behaviour = go.AddComponent<OnShowBehaviour>();

            fakeConnection = new FakeNetworkConnection();
            fakeConnection.isReady = true;

            identity.OnStartServer();
        }

        [TearDown]
        public void TearDown()
        {
            GameObject.DestroyImmediate(go);
            NetworkIdentity.spawned.Clear();
        }

        [Test]
        public void OnShownForConnectionShouldBeCalled()
        {
            NetworkConnection result = null;
            behaviour.onShown += (conn) => { result = conn; };

            visibility.SetConnectionAsVisible(fakeConnection);

            Assert.That(result, Is.EqualTo(fakeConnection), "onShown Should have been called with fake connection as the argument");
        }

        [Test]
        public void OnHiddenForConnectionShouldBeCalled()
        {
            NetworkConnection result = null;
            behaviour.onHide += (conn) => { result = conn; };

            //set it to fake conn, then clear it
            visibility.SetConnectionAsVisible(fakeConnection);
            visibility.SetConnectionAsVisible(null);

            Assert.That(result, Is.EqualTo(fakeConnection), "onHide Should have been called with fake connection as the argument");
        }
    }
}
