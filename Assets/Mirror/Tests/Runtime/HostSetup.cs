using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

using static Mirror.Tests.AsyncUtil;

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

        protected GameObject playerGO;
        protected NetworkIdentity identity;
        protected T component;

        [UnitySetUp]
        public IEnumerator SetupHost() => RunAsync(async () =>
        {
            networkManagerGo = new GameObject();
            networkManagerGo.AddComponent<MockTransport>();
            sceneManager = networkManagerGo.AddComponent<NetworkSceneManager>();
            manager = networkManagerGo.AddComponent<NetworkManager>();
            manager.client = networkManagerGo.GetComponent<NetworkClient>();
            manager.server = networkManagerGo.GetComponent<NetworkServer>();
            server = manager.server;
            client = manager.client;
            server.sceneManager = sceneManager;
            client.sceneManager = sceneManager;
            manager.startOnHeadless = false;
            sceneManager.client = client;
            sceneManager.server = server;

            // wait for client and server to initialize themselves
            await Task.Delay(1);

            // now start the host
            await manager.StartHost();

            playerGO = new GameObject("playerGO", typeof(Rigidbody));
            identity = playerGO.AddComponent<NetworkIdentity>();
            component = playerGO.AddComponent<T>();

            server.AddPlayerForConnection(server.LocalConnection, playerGO);

            client.Update();
        });

        [UnityTearDown]
        public IEnumerator ShutdownHost() => RunAsync(async () =>
        {
            Object.Destroy(playerGO);
            manager.StopHost();

            await Task.Delay(1);
            Object.Destroy(networkManagerGo);
        });

        #endregion
    }
}
