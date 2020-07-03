using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    public class NetworkManagerTest
    {
        [UnityTest]
        public IEnumerator NetworkManagerSetupHasComponents()
        {
            NetworkManager networkManager = new GameObject("Network Manager Test").AddComponent<NetworkManager>();
            yield return null;
            Assert.IsNotNull(networkManager.client);
            Assert.IsNotNull(networkManager.server);

            Object.DestroyImmediate(networkManager);
        }
    }
}
