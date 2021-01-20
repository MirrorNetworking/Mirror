using UnityEngine;
using UnityEditor;
using Mirror.KCP;

namespace Mirror
{

    public static class NetworkMenu
    {
        // Start is called before the first frame update
        [MenuItem("GameObject/Network/NetworkManager", priority = 7)]
        public static GameObject CreateNetworkManager()
        {
            var go = new GameObject("NetworkManager", typeof(NetworkManager), typeof(NetworkServer), typeof(NetworkClient), typeof(NetworkSceneManager), typeof(ServerObjectManager), typeof(ClientObjectManager), typeof(PlayerSpawner), typeof(KcpTransport), typeof(LogSettings));

            KcpTransport transport = go.GetComponent<KcpTransport>();
            NetworkSceneManager nsm = go.GetComponent<NetworkSceneManager>();

            NetworkClient networkClient = go.GetComponent<NetworkClient>();
            networkClient.Transport = transport;

            NetworkServer networkServer = go.GetComponent<NetworkServer>();
            networkServer.Transport = transport;

            ServerObjectManager serverObjectManager = go.GetComponent<ServerObjectManager>();
            serverObjectManager.server = networkServer;
            serverObjectManager.networkSceneManager = nsm;

            ClientObjectManager clientObjectManager = go.GetComponent<ClientObjectManager>();
            clientObjectManager.client = networkClient;
            clientObjectManager.networkSceneManager = nsm;

            NetworkManager networkManager = go.GetComponent<NetworkManager>();
            networkManager.client = networkClient;
            networkManager.server = networkServer;
            networkManager.serverObjectManager = serverObjectManager;
            networkManager.clientObjectManager = clientObjectManager;
            networkManager.sceneManager = nsm;

            PlayerSpawner playerSpawner = go.GetComponent<PlayerSpawner>();
            playerSpawner.client = networkClient;
            playerSpawner.server = networkServer;
            playerSpawner.sceneManager = nsm;
            playerSpawner.serverObjectManager = serverObjectManager;
            playerSpawner.clientObjectManager = clientObjectManager;

            nsm.client = networkClient;
            nsm.server = networkServer;
            return go;
        }
    }
}
