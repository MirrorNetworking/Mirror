using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime
{
    public class HostSetup
    {
        protected GameObject networkManagerGo;
        protected NetworkManager manager;
        protected MemoryTransport transport;

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
        public IEnumerator SetupHost()
        {
            networkManagerGo = new GameObject();
            transport = networkManagerGo.AddComponent<MemoryTransport>();
            manager = networkManagerGo.AddComponent<NetworkManager>();
            Transport.activeTransport = transport;

            playerGO = new GameObject();
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
        public IEnumerator ShutdownHost()
        {
            beforeStopHost();

            yield return null;

            Object.DestroyImmediate(playerGO);
            manager.StopHost();
            Object.DestroyImmediate(networkManagerGo);
        }
    }
}
