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
            manager.startOnHeadless = false;
        }

        public void ShutdownServer()
        {
            manager.StopServer();
            GameObject.DestroyImmediate(networkManagerGo);
        }

        #endregion
    }
}
