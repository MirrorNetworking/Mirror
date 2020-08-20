using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime
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
            transport = networkManagerGo.AddComponent<MemoryTransport>();
            manager = networkManagerGo.AddComponent<NetworkManager>();
            Transport.activeTransport = transport;

            playerGO = new GameObject();
            identity = playerGO.AddComponent<NetworkIdentity>();
            identity.assetId = System.Guid.NewGuid();

            manager.playerPrefab = playerGO;
            manager.autoStartServerBuild = false;

            yield return null;

            manager.StartHost();
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
