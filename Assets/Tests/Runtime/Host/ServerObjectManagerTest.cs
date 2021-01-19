using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Mirror.Tests.LocalConnections;
using Object = UnityEngine.Object;

namespace Mirror.Tests.Host
{

    [TestFixture]
    public class ServerObjectManagerHostTest : HostSetup<MockComponent>
    {
        [Test]
        public void SetClientReadyAndNotReadyTest()
        {
            (_, NetworkConnection connection) = PipedConnections();
            Assert.That(connection.IsReady, Is.False);

            serverObjectManager.SetClientReady(connection);
            Assert.That(connection.IsReady, Is.True);

            serverObjectManager.SetClientNotReady(connection);
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
            serverObjectManager.SetAllClientsNotReady();
            Assert.That(first.IsReady, Is.False);
            Assert.That(second.IsReady, Is.False);
        }

        [Test]
        public void GetNetworkIdentity()
        {
            Assert.That(serverObjectManager.GetNetworkIdentity(playerGO), Is.EqualTo(identity));
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
                _ = serverObjectManager.GetNetworkIdentity(goWithout);
            });

            // clean up
            Object.Destroy(goWithout);
        }

        [Test]
        public void HideForConnection()
        {
            // add connection

            NetworkConnection connectionToClient = Substitute.For<NetworkConnection>(Substitute.For<IConnection>());

            NetworkIdentity identity = new GameObject().AddComponent<NetworkIdentity>();

            serverObjectManager.HideForConnection(identity, connectionToClient);

            connectionToClient.Received().Send(Arg.Is<ObjectHideMessage>(msg => msg.netId == identity.NetId));

            // destroy GO after shutdown, otherwise isServer is true in OnDestroy and it tries to call
            // GameObject.Destroy (but we need DestroyImmediate in Editor)
            Object.Destroy(identity.gameObject);
        }

        [Test]
        public void ValidateSceneObject()
        {
            identity.sceneId = 42;
            Assert.That(serverObjectManager.ValidateSceneObject(identity), Is.True);
            identity.sceneId = 0;
            Assert.That(serverObjectManager.ValidateSceneObject(identity), Is.False);
        }

        [Test]
        public void HideFlagsTest()
        {
            // shouldn't be valid for certain hide flags
            playerGO.hideFlags = HideFlags.NotEditable;
            Assert.That(serverObjectManager.ValidateSceneObject(identity), Is.False);
            playerGO.hideFlags = HideFlags.HideAndDontSave;
            Assert.That(serverObjectManager.ValidateSceneObject(identity), Is.False);
        }

        [Test]
        public void UnSpawn()
        {
            // unspawn
            serverObjectManager.UnSpawn(playerGO);

            // it should have been marked for reset now
            Assert.That(identity.NetId, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DestroyAllSpawnedOnStopTest() => UniTask.ToCoroutine(async () =>
        {
            GameObject spawnTestObj = new GameObject("testObj", typeof(NetworkIdentity));
            serverObjectManager.Spawn(spawnTestObj);

            //1 is the player. should be 2 at this point
            Assert.That(server.Spawned.Count, Is.GreaterThan(1));

            server.Disconnect();

            await AsyncUtil.WaitUntilWithTimeout(() => !server.Active);

            //Value will not be 0 as the SceneObject is not destroyed in the stop process.
            Assert.That(server.Spawned.Count, Is.GreaterThan(0));
        });
    }
}
