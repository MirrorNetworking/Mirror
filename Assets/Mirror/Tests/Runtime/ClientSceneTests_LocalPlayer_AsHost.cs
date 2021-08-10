using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime
{
    public class ClientSceneTest_LocalPlayer_AsHost : MirrorPlayModeTest
    {
        protected GameObject networkManagerGo;
        protected NetworkManager manager;

        protected GameObject playerGO;
        protected NetworkIdentity identity;

        protected virtual bool AutoAddPlayer => true;

        protected virtual void afterStartHost() {}
        protected virtual void beforeStopHost() {}

        [UnitySetUp]
        public override IEnumerator UnitySetUp()
        {
            base.SetUp();

            networkManagerGo = transport.gameObject;
            manager = networkManagerGo.AddComponent<NetworkManager>();
            Transport.activeTransport = transport;

            // create a tracked prefab (not spawned)
            CreateGameObject(out playerGO);
            identity = playerGO.AddComponent<NetworkIdentity>();
            identity.assetId = System.Guid.NewGuid();

            manager.playerPrefab = playerGO;
            manager.autoStartServerBuild = false;
            manager.autoCreatePlayer = AutoAddPlayer;

            if (Application.isBatchMode)
            {
                Application.targetFrameRate = 60;
            }

            yield return null;

            manager.StartHost();

            yield return null;

            afterStartHost();
        }

        [UnityTearDown]
        public override IEnumerator UnityTearDown()
        {
            beforeStopHost();

            yield return null;

            // needed for stophost
            Transport.activeTransport = transport;
            manager.StopHost();

            base.TearDown();
        }

        [UnityTest]
        public IEnumerator LocalPlayerIsSetToNullAfterNetworkDestroy()
        {
            const uint netId = 1000;
            CreateNetworked(out GameObject go, out NetworkIdentity identity);

            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                isLocalPlayer = true,
                isOwner = true,
            };


            NetworkIdentity.spawned[msg.netId] = identity;
            NetworkClient.OnHostClientSpawn(msg);

            NetworkServer.Destroy(identity.gameObject);

            // wait a frame for destroy to happen
            yield return null;

            // use "is null" here to avoid unity == check
            Assert.IsTrue(NetworkClient.localPlayer is null, "local player should be set to c# null");
        }

        [UnityTest]
        public IEnumerator LocalPlayerIsSetToNullAfterNetworkUnspawn()
        {
            const uint netId = 1000;
            CreateNetworked(out GameObject go, out NetworkIdentity identity);

            SpawnMessage msg = new SpawnMessage
            {
                netId = netId,
                isLocalPlayer = true,
                isOwner = true,
            };

            NetworkIdentity.spawned[msg.netId] = identity;
            NetworkClient.OnHostClientSpawn(msg);

            NetworkServer.UnSpawn(identity.gameObject);

            // wait a frame for destroy to happen
            yield return null;

            // use "is null" here to avoid unity == check
            Assert.IsTrue(NetworkClient.localPlayer is null, "local player should be set to c# null");
        }
    }
}
