using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using InvalidOperationException = System.InvalidOperationException;

namespace Mirror.Tests
{
    public class NetworkIdentityTests
    {
        #region SetUp
        NetworkServer server;
        GameObject serverGO;
        NetworkClient client;
        GameObject clientGO;


        GameObject gameObject;
        NetworkIdentity identity;

        [SetUp]
        public void SetUp()
        {
            Transport.activeTransport = Substitute.For<Transport>();
            serverGO = new GameObject();
            server = serverGO.AddComponent<NetworkServer>();

            clientGO = new GameObject();
            client = clientGO.AddComponent<NetworkClient>();
            server.Listen(2);
            client.ConnectHost(server);


            gameObject = new GameObject();
            identity = gameObject.AddComponent<NetworkIdentity>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
            // reset all state
            server.Shutdown();
            Object.DestroyImmediate(serverGO);
            Object.DestroyImmediate(clientGO);
            Transport.activeTransport = null;
            server.Shutdown();
        }
        #endregion

        [Test]
        public void AssignClientAuthorityNoServer()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                identity.AssignClientAuthority(server.localConnection);
            });

            Assert.That(ex.Message, Is.EqualTo("AssignClientAuthority can only be called on the server for spawned objects"));

        }

        [Test]
        public void IsServer()
        {
            Assert.That(identity.isServer, Is.False);
            // create a networkidentity with our test component
            server.Spawn(gameObject);

            Assert.That(identity.isServer, Is.True);
        }

        [Test]
        public void AssignClientAuthorityCallback()
        {
            // create a networkidentity with our test component
            server.Spawn(gameObject);

            // test the callback too
            int callbackCalled = 0;

            void Callback(NetworkConnection conn, NetworkIdentity networkIdentity, bool state)
            {
                ++callbackCalled;
                Assert.That(networkIdentity, Is.EqualTo(identity));
                Assert.That(state, Is.True);
            }

            NetworkIdentity.clientAuthorityCallback += Callback;

            // assign authority
            identity.AssignClientAuthority(server.localConnection);

            Assert.That(callbackCalled, Is.EqualTo(1));

            NetworkIdentity.clientAuthorityCallback -= Callback;
        }

        [Test]
        public void DefaultAuthority()
        {
            // create a networkidentity with our test component
            server.Spawn(gameObject);
            Assert.That(identity.connectionToClient, Is.Null);
        }

        [Test]
        public void AssignAuthority()
        {
            // create a networkidentity with our test component
            server.Spawn(gameObject);
            identity.AssignClientAuthority(server.localConnection);

            Assert.That(identity.connectionToClient, Is.SameAs(server.localConnection));
        }

        [Test]
        public void SpawnWithAuthority()
        {
            server.Spawn(gameObject, server.localConnection);
            Assert.That(identity.connectionToClient, Is.SameAs(server.localConnection));
        }

        [Test]
        public void ReassignClientAuthority()
        {
            // create a networkidentity with our test component
            server.Spawn(gameObject);
            // assign authority
            identity.AssignClientAuthority(server.localConnection);

            // shouldn't be able to assign authority while already owned by
            // another connection
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                identity.AssignClientAuthority(new NetworkConnectionToClient(43));
            });
            Assert.That(ex.Message, Is.EqualTo("AssignClientAuthority for " + gameObject + " already has an owner. Use RemoveClientAuthority() first"));
        }

        [Test]
        public void AssignNullAuthority()
        {
            // create a networkidentity with our test component
            server.Spawn(gameObject);

            // someone might try to remove authority by assigning null.
            // make sure this fails.
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                identity.AssignClientAuthority(null);
            });
            Assert.That(ex.Message, Is.EqualTo("AssignClientAuthority for " + gameObject + " owner cannot be null. Use RemoveClientAuthority() instead"));
        }

        [Test]
        public void RemoveclientAuthorityNotSpawned()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                // shoud fail because the server is not active
                identity.RemoveClientAuthority();
            });
            Assert.That(ex.Message, Is.EqualTo("RemoveClientAuthority can only be called on the server for spawned objects"));            
        }


        [Test]
        public void RemoveClientAuthorityOfOwner()
        {
            server.AddPlayerForConnection(server.localConnection, gameObject);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                identity.RemoveClientAuthority();

            });
            Assert.That(ex.Message, Is.EqualTo("RemoveClientAuthority cannot remove authority for a player object"));
        }

        [Test]
        public void RemoveClientAuthority()
        {
            server.Spawn(gameObject);
            identity.AssignClientAuthority(server.localConnection);
            identity.RemoveClientAuthority();
            Assert.That(identity.connectionToClient, Is.Null);
        }

    }
}
