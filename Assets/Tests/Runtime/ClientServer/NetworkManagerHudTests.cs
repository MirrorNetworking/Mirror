using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mirror.Tests.ClientServer
{
    [TestFixture]
    public class NetworkManagerHudClientServerTest : ClientServerSetup<MockComponent>
    {
        GameObject gameObject;
        NetworkManagerHud networkManagerHud;
        public override void ExtraSetup()
        {
            gameObject = new GameObject("NetworkManagerHud", typeof(NetworkManagerHud));
            networkManagerHud = gameObject.GetComponent<NetworkManagerHud>();
            networkManagerHud.NetworkManager = clientGo.AddComponent<NetworkManager>();
            networkManagerHud.NetworkManager.Client = client;
            networkManagerHud.OfflineGO = new GameObject();
            networkManagerHud.OnlineGO = new GameObject();

            //Initial state in the prefab
            networkManagerHud.OfflineGO.SetActive(true);
            networkManagerHud.OnlineGO.SetActive(false);
        }

        public override void ExtraTearDown()
        {
            Object.DestroyImmediate(networkManagerHud.OfflineGO);
            Object.DestroyImmediate(networkManagerHud.OnlineGO);
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void StartClientButtonTest()
        {
            networkManagerHud.StartClientButtonHandler();
            Assert.That(networkManagerHud.OfflineGO.activeSelf, Is.False);
            Assert.That(networkManagerHud.OnlineGO.activeSelf, Is.True);
        }
    }
}
