using UnityEngine;

namespace Mirror.Tests
{
    public class ClientServerTests
    {
        #region Setup
        protected GameObject networkManagerGo;
        protected NetworkManager manager;
        protected NetworkServer server;
        protected NetworkClient client;

        protected GameObject clientNetworkManagerGo;
        protected NetworkManager clientManager;
        protected NetworkServer server2;
        protected NetworkClient client2;

        public void SetupServer()
        {
            networkManagerGo = new GameObject();
            manager = networkManagerGo.AddComponent<NetworkManager>();
            manager.client = networkManagerGo.GetComponent<NetworkClient>();
            manager.server = networkManagerGo.GetComponent<NetworkServer>();
            server = manager.server;
            client = manager.client;

            manager.autoCreatePlayer = false;
            
            manager.StartServer();
        }

        public void SetupClient()
        {
            clientNetworkManagerGo = new GameObject();
            clientManager = clientNetworkManagerGo.AddComponent<NetworkManager>();
            clientManager.client = clientNetworkManagerGo.GetComponent<NetworkClient>();
            clientManager.server = clientNetworkManagerGo.GetComponent<NetworkServer>();
            server2 = clientManager.server;
            client2 = clientManager.client;

            clientManager.StartClient("localhost");
        }

        public void ShutdownServer()
        {
            manager.StopServer();
            GameObject.DestroyImmediate(networkManagerGo);
        }

        public void ShutdownClient()
        {
            clientManager.StopClient();
            GameObject.DestroyImmediate(clientNetworkManagerGo);
        }

        #endregion
    }
}
