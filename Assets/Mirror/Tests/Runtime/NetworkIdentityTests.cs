using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using InvalidOperationException = System.InvalidOperationException;
using Object = UnityEngine.Object;
using static Mirror.Tests.AsyncUtil;
using System.Collections;
using UnityEngine.Events;
using NSubstitute;
using System;

namespace Mirror.Tests.Runtime
{
    public class NetworkIdentityTests
    {
        #region SetUp
        NetworkServer server;
        GameObject serverGO;
        NetworkClient client;


        GameObject gameObject;
        NetworkIdentity identity;

        [UnitySetUp]
        public IEnumerator SetUp() => RunAsync(async () =>
        {
            serverGO = new GameObject();
            serverGO.AddComponent<MockTransport>();
            server = serverGO.AddComponent<NetworkServer>();
            client = serverGO.AddComponent<NetworkClient>();
            await server.ListenAsync();
            client.ConnectHost(server);


            gameObject = new GameObject();
            identity = gameObject.AddComponent<NetworkIdentity>();
        });

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
            // reset all state
            server.Disconnect();
            Object.DestroyImmediate(serverGO);
        }
        #endregion

        [Test]
        public void AssignClientAuthorityNoServer()
        {
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                identity.AssignClientAuthority(server.LocalConnection);
            });

            Assert.That(ex.Message, Is.EqualTo("AssignClientAuthority can only be called on the server for spawned objects"));

        }

        [Test]
        public void IsServer()
        {
            Assert.That(identity.IsServer, Is.False);
            // create a networkidentity with our test component
            server.Spawn(gameObject);

            Assert.That(identity.IsServer, Is.True);
        }

        [Test]
        public void IsClient()
        {
            Assert.That(identity.IsClient, Is.False);
            // create a networkidentity with our test component
            server.Spawn(gameObject);

            Assert.That(identity.IsClient, Is.True);
        }

        [Test]
        public void IsLocalPlayer()
        {
            Assert.That(identity.IsLocalPlayer, Is.False);
            // create a networkidentity with our test component
            server.Spawn(gameObject);

            Assert.That(identity.IsLocalPlayer, Is.False);
        }

        [Test]
        public void AssignClientAuthorityCallback()
        {
            // create a networkidentity with our test component
            server.Spawn(gameObject);

            // test the callback too
            int callbackCalled = 0;

            void Callback(INetworkConnection conn, NetworkIdentity networkIdentity, bool state)
            {
                ++callbackCalled;
                Assert.That(networkIdentity, Is.EqualTo(identity));
                Assert.That(state, Is.True);
            }

            NetworkIdentity.clientAuthorityCallback += Callback;

            // assign authority
            identity.AssignClientAuthority(server.LocalConnection);

            Assert.That(callbackCalled, Is.EqualTo(1));

            NetworkIdentity.clientAuthorityCallback -= Callback;
        }

        [Test]
        public void DefaultAuthority()
        {
            // create a networkidentity with our test component
            server.Spawn(gameObject);
            Assert.That(identity.ConnectionToClient, Is.Null);
        }

        [Test]
        public void AssignAuthority()
        {
            // create a networkidentity with our test component
            server.Spawn(gameObject);
            identity.AssignClientAuthority(server.LocalConnection);

            Assert.That(identity.ConnectionToClient, Is.SameAs(server.LocalConnection));
        }

        [Test]
        public void SpawnWithAuthority()
        {
            server.Spawn(gameObject, server.LocalConnection);
            Assert.That(identity.ConnectionToClient, Is.SameAs(server.LocalConnection));
        }

        [Test]
        public void SpawnWithAssetId()
        {
            Guid replacementGuid = Guid.NewGuid();
            server.Spawn(gameObject, replacementGuid, server.LocalConnection);
            Assert.That(identity.AssetId, Is.EqualTo(replacementGuid));
        }

        [Test]
        public void ReassignClientAuthority()
        {
            // create a networkidentity with our test component
            server.Spawn(gameObject);
            // assign authority
            identity.AssignClientAuthority(server.LocalConnection);

            // shouldn't be able to assign authority while already owned by
            // another connection
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                identity.AssignClientAuthority(new NetworkConnection(null));
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
            server.AddPlayerForConnection(server.LocalConnection, gameObject);

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
            identity.AssignClientAuthority(server.LocalConnection);
            identity.RemoveClientAuthority();
            Assert.That(identity.ConnectionToClient, Is.Null);
        }


        [UnityTest]
        public IEnumerator OnStopServer()
        {
            server.Spawn(gameObject);

            UnityAction mockHandler = Substitute.For<UnityAction>();
            identity.OnStopServer.AddListener(mockHandler);

            server.UnSpawn(gameObject);

            yield return null;
            mockHandler.Received().Invoke();
        }
    }
}
