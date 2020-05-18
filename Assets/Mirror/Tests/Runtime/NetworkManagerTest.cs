using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Mirror.Tests.AsyncUtil;

namespace Mirror.Tests
{
    public class SimpleNetworkManager : NetworkManager
    {
        public void ClientChangeSceneExpose(string newSceneName, SceneOperation sceneOperation = SceneOperation.Normal, bool customHandling = false)
        {
            ClientChangeScene(newSceneName, sceneOperation, customHandling);
        }

        public void ServerChangeSceneExpose(string newSceneName)
        {
            ServerChangeScene(newSceneName);
        }
    }

    [TestFixture]
    public class NetworkManagerTest : HostSetup<MockComponent>
    {
        [Test]
        public void VariableTest()
        {
            Assert.That(manager.dontDestroyOnLoad, Is.True);
            Assert.That(manager.startOnHeadless, Is.False);
            Assert.That(manager.serverTickRate, Is.EqualTo(30));
            Assert.That(manager.server.MaxConnections, Is.EqualTo(4));
        }

        [Test]
        public void ClientChangeSceneExceptionTest()
        {
            SimpleNetworkManager comp = new GameObject().AddComponent<SimpleNetworkManager>();

            Assert.Throws<ArgumentNullException>(() =>
            {
                comp.ClientChangeScene(string.Empty);
            });
        }

        [Test]
        public void ServerChangeSceneExceptionTest()
        {
            SimpleNetworkManager comp = new GameObject().AddComponent<SimpleNetworkManager>();

            Assert.Throws<ArgumentNullException>(() =>
            {
                comp.ServerChangeScene(string.Empty);
            });
        }

        [Test]
        public void StartServerTest()
        {
            Assert.That(manager.IsNetworkActive, Is.True);
            Assert.That(manager.server.Active, Is.True);
        }

        [UnityTest]
        public IEnumerator StopServerTest() => RunAsync(async () =>
        {
            manager.StopServer();

            // wait for manager to stop
            await Task.Delay(1);

            Assert.That(manager.IsNetworkActive, Is.False);
        });

        [UnityTest]
        public IEnumerator StopClientTest() => RunAsync(async () =>
        {
            manager.StopClient();
            manager.StopServer();

            // wait until manager shuts down
            await Task.Delay(1);

            Assert.That(manager.IsNetworkActive, Is.False);
        });

        [Test]
        public void ServerChangeSceneTest()
        {
            AssetBundle.LoadFromFile("Assets/Mirror/Tests/Runtime/TestScene/testscene");
            manager.ServerChangeScene("testScene");

            Assert.That(manager.networkSceneName, Is.EqualTo("testScene"));
        }
    }
}
