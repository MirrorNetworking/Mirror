using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    // set's up a host
    public class HostSetup<T> where T : NetworkBehaviour
    {

        #region Setup
        protected GameObject networkManagerGo;
        protected NetworkManager manager;
        protected NetworkServer server;
        protected NetworkClient client;
        protected NetworkSceneManager sceneManager;
        protected ServerObjectManager serverObjectManager;
        protected ClientObjectManager clientObjectManager;

        protected GameObject playerGO;
        protected NetworkIdentity identity;
        protected T component;

        public virtual void ExtraSetup() { }

        [UnitySetUp]
        public IEnumerator SetupHost() => UniTask.ToCoroutine(async () =>
        {
            networkManagerGo = new GameObject();
            networkManagerGo.AddComponent<MockTransport>();
            sceneManager = networkManagerGo.AddComponent<NetworkSceneManager>();
            serverObjectManager = networkManagerGo.AddComponent<ServerObjectManager>();
            clientObjectManager = networkManagerGo.AddComponent<ClientObjectManager>();
            manager = networkManagerGo.AddComponent<NetworkManager>();
            manager.client = networkManagerGo.GetComponent<NetworkClient>();
            manager.server = networkManagerGo.GetComponent<NetworkServer>();
            server = manager.server;
            client = manager.client;
            sceneManager.client = client;
            sceneManager.server = server;
            serverObjectManager.server = server;
            serverObjectManager.networkSceneManager = sceneManager;
            serverObjectManager.Start();
            clientObjectManager.client = client;
            clientObjectManager.networkSceneManager = sceneManager;

            ExtraSetup();

            // wait for client and server to initialize themselves
            await UniTask.Delay(1);

            // now start the host
            await manager.server.StartHost(client);

            playerGO = new GameObject("playerGO", typeof(Rigidbody));
            identity = playerGO.AddComponent<NetworkIdentity>();
            component = playerGO.AddComponent<T>();

            serverObjectManager.AddPlayerForConnection(server.LocalConnection, playerGO);

            client.Update();
        });

        public virtual void ExtraTearDown() { }

        [UnityTearDown]
        public IEnumerator ShutdownHost() => UniTask.ToCoroutine(async () =>
        {
            Object.Destroy(playerGO);
            manager.server.StopHost();

            await UniTask.Delay(1);
            Object.Destroy(networkManagerGo);

            ExtraTearDown();
        });

        #endregion
    }
}
