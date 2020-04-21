using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    public class HostSetup
    {
        protected GameObject networkManagerGo;
        protected NetworkManager manager;
        protected MemoryTransport transport;

        protected GameObject playerGO;
        protected NetworkIdentity identity;

        [UnitySetUp]
        public IEnumerator SetupHost()
        {
            networkManagerGo = new GameObject();
            manager = networkManagerGo.AddComponent<NetworkManager>();
            transport = networkManagerGo.AddComponent<MemoryTransport>();
            Transport.activeTransport = transport;

            playerGO = new GameObject();
            identity = playerGO.AddComponent<NetworkIdentity>();

            manager.playerPrefab = playerGO;
            manager.startOnHeadless = false;

            yield return null;

            manager.StartHost();

            yield return null;

            NetworkClient.Update();
        }

        [TearDown]
        public void ShutdownHost()
        {
            Object.DestroyImmediate(playerGO);
            manager.StopHost();
            Object.DestroyImmediate(networkManagerGo);
        }
    }
}
