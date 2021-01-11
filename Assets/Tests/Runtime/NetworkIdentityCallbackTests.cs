using System.Collections.Generic;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

using static Mirror.Tests.LocalConnections;

namespace Mirror.Tests
{
    public class NetworkIdentityCallbackTests
    {
        #region test components
        class RebuildEmptyObserversNetworkBehaviour : NetworkVisibility
        {
            public override bool OnCheckObserver(INetworkConnection conn) { return true; }
            public override void OnRebuildObservers(HashSet<INetworkConnection> observers, bool initialize) { }
            public override void OnSetHostVisibility(bool visible)
            {
            }
        }


        #endregion

        GameObject gameObject;
        NetworkIdentity identity;
        private NetworkServer server;
        private ServerObjectManager serverObjectManager;
        private NetworkClient client;
        private GameObject networkServerGameObject;

        IConnection tconn42;
        IConnection tconn43;

        [SetUp]
        public void SetUp()
        {
            networkServerGameObject = new GameObject();
            server = networkServerGameObject.AddComponent<NetworkServer>();
            serverObjectManager = networkServerGameObject.AddComponent<ServerObjectManager>();
            serverObjectManager.server = server;
            client = networkServerGameObject.AddComponent<NetworkClient>();

            gameObject = new GameObject();
            identity = gameObject.AddComponent<NetworkIdentity>();
            identity.Server = server;
            identity.ServerObjectManager = serverObjectManager;

            tconn42 = Substitute.For<IConnection>();
            tconn43 = Substitute.For<IConnection>();
        }

        [TearDown]
        public void TearDown()
        {
            // set isServer is false. otherwise Destroy instead of
            // DestroyImmediate is called internally, giving an error in Editor
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(networkServerGameObject);
        }


        [Test]
        public void AddAllReadyServerConnectionsToObservers()
        {
            var connection1 = new NetworkConnection(tconn42) { IsReady = true };
            var connection2 = new NetworkConnection(tconn43) { IsReady = false };
            // add some server connections
            server.connections.Add(connection1);
            server.connections.Add(connection2);

            // add a host connection
            (_, IConnection localConnection) = PipeConnection.CreatePipe();

            server.SetLocalConnection(client, localConnection);
            server.LocalConnection.IsReady = true;

            // call OnStartServer so that observers dict is created
            identity.StartServer();

            // add all to observers. should have the two ready connections then.
            identity.AddAllReadyServerConnectionsToObservers();
            Assert.That(identity.observers, Is.EquivalentTo(new[] { connection1, server.LocalConnection }));

            // clean up
            server.Disconnect();
        }

        // RebuildObservers should always add the own ready connection
        // (if any). fixes https://github.com/vis2k/Mirror/issues/692
        [Test]
        public void RebuildObserversAddsOwnReadyPlayer()
        {
            // add at least one observers component, otherwise it will just add
            // all server connections
            gameObject.AddComponent<RebuildEmptyObserversNetworkBehaviour>();

            // add own player connection
            (_, NetworkConnection connection) = PipedConnections();
            connection.IsReady = true;
            identity.ConnectionToClient = connection;

            // call OnStartServer so that observers dict is created
            identity.StartServer();

            // rebuild should at least add own ready player
            identity.RebuildObservers(true);
            Assert.That(identity.observers, Does.Contain(identity.ConnectionToClient));
        }
    }
}
