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
using System.Threading.Tasks;

namespace Mirror.Tests
{
    public class NetworkIdentityTests : HostSetup<MockComponent>
    {
        #region SetUp

        GameObject gameObject;
        NetworkIdentity testIdentity;

        public override void ExtraSetup()
        {
            gameObject = new GameObject();
            testIdentity = gameObject.AddComponent<NetworkIdentity>();
        }

        public override void ExtraTearDown()
        {
            Object.Destroy(gameObject);
        }

        #endregion

        [Test]
        public void AssignClientAuthorityNoServer()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                testIdentity.AssignClientAuthority(server.LocalConnection);
            });
        }

        [Test]
        public void IsServer()
        {
            Assert.That(testIdentity.IsServer, Is.False);
            // create a networkidentity with our test component
            server.Spawn(gameObject);

            Assert.That(testIdentity.IsServer, Is.True);
        }

        [Test]
        public void IsClient()
        {
            Assert.That(testIdentity.IsClient, Is.False);
            // create a networkidentity with our test component
            server.Spawn(gameObject);

            Assert.That(testIdentity.IsClient, Is.True);
        }

        [Test]
        public void IsLocalPlayer()
        {
            Assert.That(testIdentity.IsLocalPlayer, Is.False);
            // create a networkidentity with our test component
            server.Spawn(gameObject);

            Assert.That(testIdentity.IsLocalPlayer, Is.False);
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
                Assert.That(networkIdentity, Is.EqualTo(testIdentity));
                Assert.That(state, Is.True);
            }

            NetworkIdentity.clientAuthorityCallback += Callback;

            // assign authority
            testIdentity.AssignClientAuthority(server.LocalConnection);

            Assert.That(callbackCalled, Is.EqualTo(1));

            NetworkIdentity.clientAuthorityCallback -= Callback;
        }

        [Test]
        public void DefaultAuthority()
        {
            // create a networkidentity with our test component
            server.Spawn(gameObject);
            Assert.That(testIdentity.ConnectionToClient, Is.Null);
        }

        [Test]
        public void AssignAuthority()
        {
            // create a networkidentity with our test component
            server.Spawn(gameObject);
            testIdentity.AssignClientAuthority(server.LocalConnection);

            Assert.That(testIdentity.ConnectionToClient, Is.SameAs(server.LocalConnection));
        }

        [Test]
        public void SpawnWithAuthority()
        {
            server.Spawn(gameObject, server.LocalConnection);
            Assert.That(testIdentity.ConnectionToClient, Is.SameAs(server.LocalConnection));
        }

        [Test]
        public void SpawnWithAssetId()
        {
            Guid replacementGuid = Guid.NewGuid();
            server.Spawn(gameObject, replacementGuid, server.LocalConnection);
            Assert.That(testIdentity.AssetId, Is.EqualTo(replacementGuid));
        }

        [Test]
        public void ReassignClientAuthority()
        {
            // create a networkidentity with our test component
            server.Spawn(gameObject);
            // assign authority
            testIdentity.AssignClientAuthority(server.LocalConnection);

            // shouldn't be able to assign authority while already owned by
            // another connection
            Assert.Throws<InvalidOperationException>(() =>
            {
                testIdentity.AssignClientAuthority(new NetworkConnection(null));
            });
        }

        [Test]
        public void AssignNullAuthority()
        {
            // create a networkidentity with our test component
            server.Spawn(gameObject);

            // someone might try to remove authority by assigning null.
            // make sure this fails.
            Assert.Throws<InvalidOperationException>(() =>
            {
                testIdentity.AssignClientAuthority(null);
            });
        }

        [Test]
        public void RemoveclientAuthorityNotSpawned()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                // shoud fail because the server is not active
                testIdentity.RemoveClientAuthority();
            });
        }

        [Test]
        public void RemoveClientAuthorityOfOwner()
        {
            server.AddPlayerForConnection(server.LocalConnection, gameObject);

            Assert.Throws<InvalidOperationException>(() =>
            {
                testIdentity.RemoveClientAuthority();
            });
        }

        [Test]
        public void RemoveClientAuthority()
        {
            server.Spawn(gameObject);
            testIdentity.AssignClientAuthority(server.LocalConnection);
            testIdentity.RemoveClientAuthority();
            Assert.That(testIdentity.ConnectionToClient, Is.Null);
        }

        [UnityTest]
        public IEnumerator OnStopServer() => RunAsync(async () =>
        {
            server.Spawn(gameObject);

            UnityAction mockHandler = Substitute.For<UnityAction>();
            testIdentity.OnStopServer.AddListener(mockHandler);

            server.UnSpawn(gameObject);

            await Task.Delay(1);
            mockHandler.Received().Invoke();
        });

        [Test]
        public void IdentityClientValueSet()
        {
            Assert.That(identity.Client, Is.Not.Null);
        }

        [Test]
        public void IdentityServerValueSet()
        {
            Assert.That(identity.Server, Is.Not.Null);
        }
    }
}
