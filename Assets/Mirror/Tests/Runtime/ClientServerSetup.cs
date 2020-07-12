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
    // set's up a client and a server
    public class ClientServerSetup<T> where T : NetworkBehaviour
    {

        #region Setup
        protected GameObject serverGo;
        protected NetworkServer server;
        protected NetworkSceneManager serverSceneManager;
        protected GameObject serverPlayerGO;
        protected NetworkIdentity serverIdentity;
        protected T serverComponent;

        protected GameObject clientGo;
        protected NetworkClient client;
        protected NetworkSceneManager clientSceneManager;
        protected GameObject clientPlayerGO;
        protected NetworkIdentity clientIdentity;
        protected T clientComponent;

        private GameObject playerPrefab;

        protected Transport testTransport;
        protected INetworkConnection connectionToServer;
        protected INetworkConnection connectionToClient;

        public virtual void ExtraSetup()
        {

        }

        [UnitySetUp]
        public IEnumerator Setup() => RunAsync(async () =>
        {
            serverGo = new GameObject("server", typeof(NetworkSceneManager), typeof(NetworkServer));
            clientGo = new GameObject("client", typeof(NetworkSceneManager), typeof(NetworkClient));
            testTransport = serverGo.AddComponent<LoopbackTransport>();

            await Task.Delay(1);

            server = serverGo.GetComponent<NetworkServer>();
            client = clientGo.GetComponent<NetworkClient>();

            server.transport = testTransport;
            client.Transport = testTransport;

            serverSceneManager = serverGo.GetComponent<NetworkSceneManager>();
            clientSceneManager = clientGo.GetComponent<NetworkSceneManager>();

            server.sceneManager = serverSceneManager;
            client.sceneManager = clientSceneManager;

            serverSceneManager.server = server;
            clientSceneManager.client = client;

            ExtraSetup();

            // create and register a prefab
            playerPrefab = new GameObject("serverPlayer", typeof(NetworkIdentity), typeof(T));
            playerPrefab.GetComponent<NetworkIdentity>().AssetId = Guid.NewGuid();
            client.RegisterPrefab(playerPrefab);

            // wait for client and server to initialize themselves
            await Task.Delay(1);

            // start the server
            await server.ListenAsync();

            await Task.Delay(1);

            var builder = new UriBuilder
            {
                Host = "localhost",
                Scheme = client.Transport.Scheme,
            };

            // now start the client
            await client.ConnectAsync(builder.Uri);

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
            client.Disconnect();
            server.Disconnect();

            await WaitFor(() => !client.Active);
            await WaitFor(() => !server.Active);

            Object.DestroyImmediate(playerPrefab);
            Object.DestroyImmediate(serverGo);
            Object.DestroyImmediate(clientGo);
            Object.DestroyImmediate(serverPlayerGO);
            Object.DestroyImmediate(clientPlayerGO);
        });

        #endregion
    }
}
