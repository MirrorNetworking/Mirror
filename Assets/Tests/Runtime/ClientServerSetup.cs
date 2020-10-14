using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

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

        public virtual void ExtraSetup() { }

        [UnitySetUp]
        public IEnumerator Setup() => UniTask.ToCoroutine(async () =>
        {
            serverGo = new GameObject("server", typeof(NetworkSceneManager), typeof(NetworkServer));
            clientGo = new GameObject("client", typeof(NetworkSceneManager), typeof(NetworkClient));
            testTransport = serverGo.AddComponent<LoopbackTransport>();

            await UniTask.Delay(1);

            server = serverGo.GetComponent<NetworkServer>();
            client = clientGo.GetComponent<NetworkClient>();

            server.transport = testTransport;
            client.Transport = testTransport;

            serverSceneManager = serverGo.GetComponent<NetworkSceneManager>();
            clientSceneManager = clientGo.GetComponent<NetworkSceneManager>();

            serverSceneManager.server = server;
            clientSceneManager.client = client;

            ExtraSetup();

            // create and register a prefab
            playerPrefab = new GameObject("serverPlayer", typeof(NetworkIdentity), typeof(T));
            playerPrefab.GetComponent<NetworkIdentity>().AssetId = Guid.NewGuid();
            client.RegisterPrefab(playerPrefab);

            // wait for client and server to initialize themselves
            await UniTask.Delay(1);

            // start the server
            await server.ListenAsync();

            await UniTask.Delay(1);

            var builder = new UriBuilder
            {
                Host = "localhost",
                Scheme = client.Transport.Scheme.First(),
            };

            // now start the client
            await client.ConnectAsync(builder.Uri);

            await UniTask.WaitUntil(() => server.connections.Count > 0);

            // get the connections so that we can spawn players
            connectionToClient = server.connections.First();
            connectionToServer = client.Connection;

            // create a player object in the server
            serverPlayerGO = Object.Instantiate(playerPrefab);
            serverIdentity = serverPlayerGO.GetComponent<NetworkIdentity>();
            serverComponent = serverPlayerGO.GetComponent<T>();
            server.AddPlayerForConnection(connectionToClient, serverPlayerGO);

            // wait for client to spawn it
            await UniTask.WaitUntil(() => connectionToServer.Identity != null);

            clientIdentity = connectionToServer.Identity;
            clientPlayerGO = clientIdentity.gameObject;
            clientComponent = clientPlayerGO.GetComponent<T>();
        });

        public virtual void ExtraTearDown() { }

        [UnityTearDown]
        public IEnumerator ShutdownHost() => UniTask.ToCoroutine(async () =>
        {
            client.Disconnect();
            server.Disconnect();

            await UniTask.WaitUntil(() => !client.Active);
            await UniTask.WaitUntil(() => !server.Active);

            Object.DestroyImmediate(playerPrefab);
            Object.DestroyImmediate(serverGo);
            Object.DestroyImmediate(clientGo);
            Object.DestroyImmediate(serverPlayerGO);
            Object.DestroyImmediate(clientPlayerGO);

            ExtraTearDown();
        });

        #endregion
    }
}
