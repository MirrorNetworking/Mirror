using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime
{
    public class HostSetup : MirrorPlayModeTest
    {
        protected GameObject networkManagerGo;
        protected NetworkManager manager;

        protected GameObject playerGO;
        protected NetworkIdentity identity;

        protected virtual bool AutoAddPlayer => true;

        protected virtual void afterStartHost() {}
        protected virtual void beforeStopHost() {}

        protected static void FakeSpawnServerClientIdentity(NetworkIdentity serverNI, NetworkIdentity clientNI)
        {
            serverNI.OnStartServer();
            NetworkServer.RebuildObservers(serverNI, true);

            clientNI.netId = serverNI.netId;
            NetworkIdentity.spawned[serverNI.netId] = clientNI;
            clientNI.OnStartClient();
        }

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
    }
}
