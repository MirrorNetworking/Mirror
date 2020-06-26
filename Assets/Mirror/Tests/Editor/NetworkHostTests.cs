using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class NetworkHostTests : NetworkClientTestsBase
    {
        [Test]
        public void LocalConnectionHasAddressEqualToLocalHost()
        {
            NetworkHost.ConnectHost();
            Assert.That(NetworkClient.serverIp, Is.EqualTo("localhost"));
        }

        [Test]
        public void IsConnectedAfterHostConnect()
        {
            Assert.That(NetworkClient.isConnected, Is.False);
            NetworkHost.ConnectHost();
            Assert.That(NetworkClient.isConnected, Is.True);
        }



        [Test]
        public void IsNotConnectedAfterHostDisconnect()
        {
            NetworkHost.ConnectHost();
            Assert.That(NetworkClient.isConnected, Is.True);
            Assert.That(NetworkServer.localConnection, !Is.Null);

            NetworkClient.Disconnect();
            Assert.That(NetworkClient.isConnected, Is.False);
            Assert.That(NetworkServer.localConnection, Is.Null);
        }

        [Test]
        public void ActivateHostSceneCallsOnStartClient()
        {
            // add an identity with a networkbehaviour to .spawned
            GameObject go = new GameObject();
            NetworkIdentity identity = go.AddComponent<NetworkIdentity>();
            identity.netId = 42;
            // for authority check
            //identity.connectionToClient = connection;
            OnStartClientTestNetworkBehaviour comp = go.AddComponent<OnStartClientTestNetworkBehaviour>();
            Assert.That(comp.called, Is.EqualTo(0));
            //connection.identity = identity;
            NetworkIdentity.spawned[identity.netId] = identity;

            // ActivateHostScene
            NetworkHost.ActivateHostScene();

            // was OnStartClient called for all .spawned networkidentities?
            Assert.That(comp.called, Is.EqualTo(1));

            // clean up
            NetworkIdentity.spawned.Clear();
            NetworkServer.Shutdown();
            // destroy the test gameobject AFTER server was stopped.
            // otherwise isServer is true in OnDestroy, which means it would try
            // to call Destroy(go). but we need to use DestroyImmediate in
            // Editor
            GameObject.DestroyImmediate(go);
        }
    }
}
