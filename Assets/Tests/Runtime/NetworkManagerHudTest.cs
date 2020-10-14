using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Mirror.Tests
{
    [TestFixture]
    public class NetworkManagerHudTest : HostSetup<MockComponent>
    {
        GameObject gameObject;
        NetworkManagerHud networkManagerHud;
        public override void ExtraSetup()
        {
            gameObject = new GameObject("NetworkManagerHud", typeof(NetworkManagerHud));
            networkManagerHud = gameObject.GetComponent<NetworkManagerHud>();
            networkManagerHud.NetworkManager = manager;
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
        public void OnlineSetActiveTest()
        {
            networkManagerHud.OnlineSetActive();
            Assert.That(networkManagerHud.OfflineGO.activeSelf, Is.False);
            Assert.That(networkManagerHud.OnlineGO.activeSelf, Is.True);
        }

        [Test]
        public void OfflineSetActiveTest()
        {
            networkManagerHud.OfflineSetActive();
            Assert.That(networkManagerHud.OfflineGO.activeSelf, Is.True);
            Assert.That(networkManagerHud.OnlineGO.activeSelf, Is.False);
        }

        [Test]
        public void StartHostButtonTest()
        {
            networkManagerHud.StartHostButtonHandler();
            Assert.That(networkManagerHud.OfflineGO.activeSelf, Is.False);
            Assert.That(networkManagerHud.OnlineGO.activeSelf, Is.True);

            Assert.That(manager.server.Active, Is.True);
            Assert.That(manager.client.Active, Is.True);
        }

        [Test]
        public void StartServerOnlyButtonTest()
        {
            networkManagerHud.StartServerOnlyButtonHandler();
            Assert.That(networkManagerHud.OfflineGO.activeSelf, Is.False);
            Assert.That(networkManagerHud.OnlineGO.activeSelf, Is.True);

            Assert.That(manager.server.Active, Is.True);
        }

        [UnityTest]
        public IEnumerator StopButtonTest() => UniTask.ToCoroutine(async () =>
        {
            networkManagerHud.StopButtonHandler();
            Assert.That(networkManagerHud.OfflineGO.activeSelf, Is.True);
            Assert.That(networkManagerHud.OnlineGO.activeSelf, Is.False);

            await UniTask.WaitUntil(() => !manager.IsNetworkActive);

            Assert.That(manager.IsNetworkActive, Is.False);
        });
    }

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
            networkManagerHud.NetworkManager.client = client;
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
