using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

using static Mirror.Tests.AsyncUtil;
using Object = UnityEngine.Object;

namespace Mirror.Tests
{
    // set's up a host
    public class ClientServerSetup<T> where T : NetworkBehaviour
    {

        #region Setup
        protected GameObject networkManagerGo;
        protected NetworkManager manager;
        protected NetworkServer server;
        protected NetworkClient client;
        protected NetworkSceneManager sceneManager;

        protected GameObject serverPlayerGO;
        protected NetworkIdentity serverIdentity;
        protected T serverComponent;

        protected GameObject clientPlayerGO;
        protected NetworkIdentity clientIdentity;
        protected T clientComponent;

        private GameObject playerPrefab;
        protected INetworkConnection connectionToServer;
        protected INetworkConnection connectionToClient;

        public virtual void ExtraSetup()
        {

        }

        [UnitySetUp]
        public IEnumerator Setup() => RunAsync(async () =>
        {
            networkManagerGo = new GameObject("NetworkManager", typeof(LoopbackTransport), typeof(NetworkSceneManager), typeof(NetworkClient), typeof(NetworkServer), typeof(NetworkManager) );

            sceneManager = networkManagerGo.GetComponent<NetworkSceneManager>();
            manager = networkManagerGo.GetComponent<NetworkManager>();
            manager.client = networkManagerGo.GetComponent<NetworkClient>();
            manager.server = networkManagerGo.GetComponent<NetworkServer>();
            Transport transport = networkManagerGo.GetComponent<Transport>();

            manager.transport = transport;
            server = manager.server;
            client = manager.client;
            client.Transport = transport;
            server.transport = transport;
            server.sceneManager = sceneManager;
            client.sceneManager = sceneManager;
            manager.startOnHeadless = false;

            sceneManager.client = client;
            sceneManager.server = server;

            ExtraSetup();

            // create and register a prefab
            playerPrefab = new GameObject("serverPlayer", typeof(NetworkIdentity), typeof(T));
            playerPrefab.GetComponent<NetworkIdentity>().AssetId = Guid.NewGuid();
            client.RegisterPrefab(playerPrefab);

            // wait for client and server to initialize themselves
            await Task.Delay(1);

            // start the server
            await manager.StartServer();

            // now start the client
            await manager.StartClient("localhost");

            // get the connections so that we can spawn players
            connectionToClient = server.connections.First();
            connectionToServer = client.Connection;

            // create a player object in the server
            serverPlayerGO = GameObject.Instantiate(playerPrefab);
            serverIdentity = serverPlayerGO.GetComponent<NetworkIdentity>();
            serverComponent = serverPlayerGO.GetComponent<T>();
            server.AddPlayerForConnection(connectionToClient, serverPlayerGO);

            // wait for client to spawn it
            await WaitFor(() => connectionToServer.Identity != null);

            clientIdentity = connectionToServer.Identity;
            clientPlayerGO = clientIdentity.gameObject;
            clientComponent = clientPlayerGO.GetComponent<T>();
        });

        [UnityTearDown]
        public IEnumerator ShutdownHost() => RunAsync(async () =>
        {
            manager.StopClient();
            manager.StopServer();

            await WaitFor(() => !server.Active);

            Object.Destroy(playerPrefab);
            Object.Destroy(networkManagerGo);
            Object.Destroy(serverPlayerGO);
            Object.Destroy(clientPlayerGO);
        });


        #endregion
    }
}
