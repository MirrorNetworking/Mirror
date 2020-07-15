using System;
using System.Collections;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

using static Mirror.Tests.AsyncUtil;

namespace Mirror.Tests
{
    public class NetworkSceneManagerTests : HostSetup<MockComponent>
    {
        [Test]
        public void FinishLoadSceneHostTest()
        {
            UnityAction<INetworkConnection> func1 = Substitute.For<UnityAction<INetworkConnection>>();
            UnityAction<string> func2 = Substitute.For<UnityAction<string>>();
            UnityAction<INetworkConnection> func3 = Substitute.For<UnityAction<INetworkConnection>>();

            client.Authenticated.AddListener(func1);
            sceneManager.ServerSceneChanged.AddListener(func2);
            sceneManager.ClientSceneChanged.AddListener(func3);

            sceneManager.FinishLoadScene();

            func1.Received(1).Invoke(Arg.Any<INetworkConnection>());
            func2.Received(1).Invoke(Arg.Any<string>());
            func3.Received(1).Invoke(Arg.Any<INetworkConnection>());
        }

        int onOnServerSceneOnlyChangedCounter;
        void TestOnServerOnlySceneChangedInvoke(string scene)
        {
            onOnServerSceneOnlyChangedCounter++;
        }

        [UnityTest]
        public IEnumerator FinishLoadServerOnlyTest() => RunAsync(async () =>
        {
            client.Disconnect();

            await Task.Delay(1);

            sceneManager.ServerSceneChanged.AddListener(TestOnServerOnlySceneChangedInvoke);

            sceneManager.FinishLoadScene();

            Assert.That(onOnServerSceneOnlyChangedCounter, Is.EqualTo(1));
        });

        int OnServerChangeSceneCounter;
        void TestOnServerChangeSceneInvoke(string scene)
        {
            OnServerChangeSceneCounter++;
        }

        int ClientSceneMessageCounter;
        void ClientSceneMessage(INetworkConnection conn, SceneMessage msg)
        {
            ClientSceneMessageCounter++;
        }

        int NotReadyMessageCounter;
        void NotReadyMessage(INetworkConnection conn, NotReadyMessage msg)
        {
            NotReadyMessageCounter++;
        }

        [UnityTest]
        public IEnumerator ServerChangeSceneTest() => RunAsync(async () =>
        {
            client.Connection.RegisterHandler<SceneMessage>(ClientSceneMessage);
            client.Connection.RegisterHandler<NotReadyMessage>(NotReadyMessage);
            sceneManager.ServerChangeScene.AddListener(TestOnServerChangeSceneInvoke);

            AssetBundle.LoadFromFile("Assets/Mirror/Tests/Runtime/TestScene/testscene");
            server.sceneManager.ChangeServerScene("testScene");

            Assert.That(server.sceneManager.networkSceneName, Is.EqualTo("testScene"));
            Assert.That(OnServerChangeSceneCounter, Is.EqualTo(1));

            await WaitFor(() => ClientSceneMessageCounter > 0 && NotReadyMessageCounter > 0);

            Assert.That(ClientSceneMessageCounter, Is.EqualTo(1));
            Assert.That(NotReadyMessageCounter, Is.EqualTo(1));
        });

        [Test]
        public void ChangeServerSceneExceptionTest()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                server.sceneManager.ChangeServerScene(string.Empty);
            });
        }

        [Test]
        public void ReadyTest()
        {
            client.sceneManager.SetClientReady(client.Connection);
            Assert.That(sceneManager.Ready);
            Assert.That(client.Connection.IsReady);
        }

        [Test]
        public void ReadyTwiceTest()
        {
            sceneManager.SetClientReady(client.Connection);

            Assert.Throws<InvalidOperationException>(() =>
            {
                sceneManager.SetClientReady(client.Connection);
            });
        }

        [Test]
        public void ReadyNull()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                sceneManager.SetClientReady(null);
            });
        }

        [Test]
        public void HostModeClientSceneException()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                sceneManager.ClientSceneMessage(null, new SceneMessage());
            });
        }

        int ClientChangeCalled;
        public void ClientChangeScene(string sceneName, SceneOperation sceneOperation)
        {
            ClientChangeCalled++;
        }

        [Test]
        public void ClientChangeSceneTest()
        {
            sceneManager.ClientChangeScene.AddListener(ClientChangeScene);
            sceneManager.OnClientChangeScene("", SceneOperation.Normal);
            Assert.That(ClientChangeCalled, Is.EqualTo(1));
        }

        [Test]
        public void ClientSceneChangedTest()
        {
            UnityAction<INetworkConnection> func1 = Substitute.For<UnityAction<INetworkConnection>>();
            sceneManager.ClientSceneChanged.AddListener(func1);
            sceneManager.OnClientSceneChanged(client.Connection);
            func1.Received(1).Invoke(Arg.Any<INetworkConnection>());
        }

        [Test]
        public void ClientNotReadyTest()
        {
            UnityAction<INetworkConnection> func1 = Substitute.For<UnityAction<INetworkConnection>>();
            sceneManager.ClientNotReady.AddListener(func1);
            sceneManager.OnClientNotReady(client.Connection);
            func1.Received(1).Invoke(Arg.Any<INetworkConnection>());
        }
    }

    public class NetworkSceneManagerNonHostTests : ClientServerSetup<MockComponent>
    {
        [Test]
        public void ClientSceneMessageExceptionTest()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                client.sceneManager.ClientSceneMessage(null, new SceneMessage());
            });
        }

        [Test]
        public void FinishLoadSceneHostTest()
        {
            UnityAction<INetworkConnection> func1 = Substitute.For<UnityAction<INetworkConnection>>();
            UnityAction<INetworkConnection> func2 = Substitute.For<UnityAction<INetworkConnection>>();

            client.Authenticated.AddListener(func1);
            client.sceneManager.ClientSceneChanged.AddListener(func2);

            client.sceneManager.FinishLoadScene();

            func1.Received(1).Invoke(Arg.Any<INetworkConnection>());
            func2.Received(1).Invoke(Arg.Any<INetworkConnection>());
        }

        [UnityTest]
        public IEnumerator ClientOfflineSceneException() => RunAsync(async () =>
        {
            client.Disconnect();

            await WaitFor(() => !client.Active);

            Assert.Throws<InvalidOperationException>(() =>
            {
                client.sceneManager.ClientSceneMessage(null, new SceneMessage());
            });
        });

        [Test]
        public void ClientNotReady()
        {
            UnityAction<INetworkConnection> func1 = Substitute.For<UnityAction<INetworkConnection>>();
            client.sceneManager.ClientNotReady.AddListener(func1);
            client.sceneManager.SetClientReady(client.Connection);
            client.sceneManager.ClientNotReadyMessage(null, new NotReadyMessage());

            Assert.That(client.sceneManager.Ready, Is.False);
            func1.Received(1).Invoke(Arg.Any<INetworkConnection>());
        }
    }
}
