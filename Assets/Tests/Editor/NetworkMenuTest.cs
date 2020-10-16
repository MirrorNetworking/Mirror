using Mirror.KCP;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class NetworkMenuTest
    {
        [Test]
        public void NetworkMenuTestSimplePasses()
        {
            GameObject go = NetworkMenu.CreateNetworkManager();

            Assert.That(go.GetComponent<NetworkManager>, Is.Not.Null);
            Assert.That(go.GetComponent<NetworkServer>, Is.Not.Null);
            Assert.That(go.GetComponent<NetworkClient>, Is.Not.Null);
            Assert.That(go.GetComponent<KcpTransport>, Is.Not.Null);
            Assert.That(go.GetComponent<NetworkSceneManager>, Is.Not.Null);
            // Use the Assert class to test conditions

            Object.DestroyImmediate(go);
        }
    }
}
