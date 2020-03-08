using System.Collections;
using System.Collections.Generic;
using Mirror.Tcp;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{

    // set's up a host

    public class HostTests
    {

        #region Setup
        protected GameObject networkManagerGo;
        protected NetworkManager manager;
        protected NetworkServer server;
        protected NetworkClient client;

        public void SetupHost()
        {
            networkManagerGo = new GameObject();
            manager = networkManagerGo.AddComponent<NetworkManager>();
            manager.client = networkManagerGo.GetComponent<NetworkClient>();
            manager.server = networkManagerGo.GetComponent<NetworkServer>();
            server = manager.server;
            client = manager.client;

            Transport transport = networkManagerGo.AddComponent<TcpTransport>();
            Transport.activeTransport = transport;

            manager.autoCreatePlayer = false;

            manager.StartHost();
        }

        public void ShutdownHost()
        {
            manager.StopHost();
            GameObject.DestroyImmediate(networkManagerGo);
        }

        #endregion
    }
}
