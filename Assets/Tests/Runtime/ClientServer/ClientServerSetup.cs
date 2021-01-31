using System;
using System.Collections;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

using Object = UnityEngine.Object;

namespace Mirror.Tests.ClientServer
{
    // set's up a client and a server
    public class ClientServerSetup<T> where T : NetworkBehaviour
    {

        #region Setup
        protected GameObject serverGo;
        protected NetworkServer server;
        protected NetworkSceneManager serverSceneManager;
        protected ServerObjectManager serverObjectManager;
        protected GameObject serverPlayerGO;
        protected NetworkIdentity serverIdentity;
        protected T serverComponent;

        protected GameObject clientGo;
        protected NetworkClient client;
        protected NetworkSceneManager clientSceneManager;
        protected ClientObjectManager clientObjectManager;
        protected GameObject clientPlayerGO;
        protected NetworkIdentity clientIdentity;
        protected T clientComponent;

        protected GameObject playerPrefab;

        protected Transport testTransport;
        protected INetworkConnection connectionToServer;
        protected INetworkConnection connectionToClient;

        public virtual void ExtraSetup() { }

        [UnitySetUp]
        public IEnumerator Setup() => UniTask.ToCoroutine(async () =>
        {
            serverGo = new GameObject("server", typeof(NetworkSceneManager), typeof(ServerObjectManager), typeof(NetworkServer));
            clientGo = new GameObject("client", typeof(NetworkSceneManager), typeof(ClientObjectManager), typeof(NetworkClient));
            testTransport = serverGo.AddComponent<LoopbackTransport>();

            await UniTask.Delay(1);

            server = serverGo.GetComponent<NetworkServer>();
            client = clientGo.GetComponent<NetworkClient>();

            server.Transport = testTransport;
            client.Transport = testTransport;

            serverSceneManager = serverGo.GetComponent<NetworkSceneManager>();
            clientSceneManager = clientGo.GetComponent<NetworkSceneManager>();
            serverSceneManager.Server = server;
            clientSceneManager.Client = client;
            serverSceneManager.Start();
            clientSceneManager.Start();

            serverObjectManager = serverGo.GetComponent<ServerObjectManager>();
            serverObjectManager.Server = server;
            serverObjectManager.NetworkSceneManager = serverSceneManager;
            serverObjectManager.Start();

            clientObjectManager = clientGo.GetComponent<ClientObjectManager>();
            clientObjectManager.Client = client;
            clientObjectManager.NetworkSceneManager = clientSceneManager;
            clientObjectManager.Start();

            ExtraSetup();

            // create and register a prefab
            playerPrefab = new GameObject("serverPlayer", typeof(NetworkIdentity), typeof(T));
            NetworkIdentity identity = playerPrefab.GetComponent<NetworkIdentity>();
            identity.AssetId = Guid.NewGuid();
            clientObjectManager.RegisterPrefab(identity);

            // wait for client and server to initialize themselves
            await UniTask.Delay(1);

            // start the server
            var started = new UniTaskCompletionSource();
            server.Started.AddListener(() => started.TrySetResult());
            server.ListenAsync().Forget();

            await started.Task;

            // now start the client
            await client.ConnectAsync("localhost");

            await AsyncUtil.WaitUntilWithTimeout(() => server.connections.Count > 0);

            // get the connections so that we can spawn players
            connectionToClient = server.connections.First();
            connectionToServer = client.Connection;

            // create a player object in the server
            serverPlayerGO = Object.Instantiate(playerPrefab);
            serverIdentity = serverPlayerGO.GetComponent<NetworkIdentity>();
            serverComponent = serverPlayerGO.GetComponent<T>();
            serverObjectManager.AddPlayerForConnection(connectionToClient, serverPlayerGO);

            // wait for client to spawn it
            await AsyncUtil.WaitUntilWithTimeout(() => connectionToServer.Identity != null);

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

            await AsyncUtil.WaitUntilWithTimeout(() => !client.Active);
            await AsyncUtil.WaitUntilWithTimeout(() => !server.Active);

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
