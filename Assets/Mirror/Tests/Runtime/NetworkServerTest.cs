using System;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using static Mirror.Tests.LocalConnections;
using Object = UnityEngine.Object;

using System.Linq;

namespace Mirror.Tests
{

    struct WovenTestMessage : IMessageBase
    {
        public int IntValue;
        public string StringValue;
        public double DoubleValue;

        public void Deserialize(NetworkReader reader) { }
        public void Serialize(NetworkWriter writer) { }
    }

    [TestFixture]
    public class NetworkServerTest : HostSetup<MockComponent>
    {
        [Test]
        public void MaxConnectionsTest()
        {
            GameObject secondGO = new GameObject();
            NetworkClient secondClient = secondGO.AddComponent<NetworkClient>();
            Transport secondTestTransport = secondGO.AddComponent<LoopbackTransport>();

            secondClient.Transport = secondTestTransport;

            var builder = new UriBuilder
            {
                Host = "localhost",
                Scheme = secondClient.Transport.Scheme.First(),
            };

            _ = secondClient.ConnectAsync(builder.Uri);

            Assert.That(server.connections, Has.Count.EqualTo(1));

            Object.Destroy(secondGO);
        }

        [Test]
        public void LocalClientActiveTest()
        {
            Assert.That(server.LocalClientActive, Is.True);
        }

        [Test]
        public void SetLocalConnectionExceptionTest()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                server.SetLocalConnection(null, null);
            });
        }

        [Test]
        public void SetClientReadyAndNotReadyTest()
        {
            (_, NetworkConnection connection) = PipedConnections();
            Assert.That(connection.IsReady, Is.False);

            server.SetClientReady(connection);
            Assert.That(connection.IsReady, Is.True);

            server.SetClientNotReady(connection);
            Assert.That(connection.IsReady, Is.False);
        }

        [Test]
        public void SetAllClientsNotReadyTest()
        {
            // add first ready client
            (_, NetworkConnection first) = PipedConnections();
            first.IsReady = true;
            server.connections.Add(first);

            // add second ready client
            (_, NetworkConnection second) = PipedConnections();
            second.IsReady = true;
            server.connections.Add(second);

            // set all not ready
            server.SetAllClientsNotReady();
            Assert.That(first.IsReady, Is.False);
            Assert.That(second.IsReady, Is.False);
        }

        [Test]
        public void GetNetworkIdentity()
        {
            Assert.That(server.GetNetworkIdentity(playerGO), Is.EqualTo(identity));
        }

        [Test]
        public void GetNoNetworkIdentity()
        {
            // create a GameObject without NetworkIdentity
            var goWithout = new GameObject();

            // GetNetworkIdentity for GO without identity
            // (error log is expected)
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = server.GetNetworkIdentity(goWithout);
            });

            // clean up
            Object.Destroy(goWithout);
        }

        [Test]
        public void HideForConnection()
        {
            // add connection

            NetworkConnection connectionToClient = Substitute.For<NetworkConnection>((IConnection)null);

            NetworkIdentity identity = new GameObject().AddComponent<NetworkIdentity>();

            server.HideForConnection(identity, connectionToClient);

            connectionToClient.Received().Send(Arg.Is<ObjectHideMessage>(msg => msg.netId == identity.NetId));

            // destroy GO after shutdown, otherwise isServer is true in OnDestroy and it tries to call
            // GameObject.Destroy (but we need DestroyImmediate in Editor)
            Object.Destroy(identity.gameObject);
        }

        [Test]
        public void ValidateSceneObject()
        {
            identity.sceneId = 42;
            Assert.That(server.ValidateSceneObject(identity), Is.True);
            identity.sceneId = 0;
            Assert.That(server.ValidateSceneObject(identity), Is.False);
        }

        [Test]
        public void HideFlagsTest()
        {
            // shouldn't be valid for certain hide flags
            playerGO.hideFlags = HideFlags.NotEditable;
            Assert.That(server.ValidateSceneObject(identity), Is.False);
            playerGO.hideFlags = HideFlags.HideAndDontSave;
            Assert.That(server.ValidateSceneObject(identity), Is.False);
        }

        [Test]
        public void UnSpawn()
        {
            // unspawn
            server.UnSpawn(playerGO);

            // it should have been marked for reset now
            Assert.That(identity.NetId, Is.Zero);
        }
    }
}
