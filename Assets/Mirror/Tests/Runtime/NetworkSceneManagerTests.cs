using System;
using System.Collections;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

using static Mirror.Tests.AsyncUtil;

namespace Mirror.Tests
{
    public class NetworkSceneManagerTests : HostSetup<MockComponent>
    {
        AssetBundle bundle;

        public override void ExtraSetup()
        {
            bundle = AssetBundle.LoadFromFile("Assets/Mirror/Tests/Runtime/TestScene/testscene");
        }

        public override void ExtraTearDown()
        {
            bundle.Unload(true);
        }

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

            await WaitFor(() => !client.Active);

            sceneManager.ServerSceneChanged.AddListener(TestOnServerOnlySceneChangedInvoke);

            sceneManager.FinishLoadScene();

            Assert.That(onOnServerSceneOnlyChangedCounter, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator ServerChangeSceneTest() => RunAsync(async () =>
        {
            bool invokeClientSceneMessage = false;
            bool invokeNotReadyMessage = false;
            UnityAction<string> func1 = Substitute.For<UnityAction<string>>();
            client.Connection.RegisterHandler<SceneMessage>(msg => invokeClientSceneMessage = true);
            client.Connection.RegisterHandler<NotReadyMessage>(msg => invokeNotReadyMessage = true);
            sceneManager.ServerChangeScene.AddListener(func1);

            server.sceneManager.ChangeServerScene("testScene");

            await WaitFor(() => invokeClientSceneMessage == true && invokeNotReadyMessage == true);

            func1.Received(1).Invoke(Arg.Any<string>());
            Assert.That(server.sceneManager.NetworkSceneName, Is.EqualTo("testScene"));
            Assert.That(invokeClientSceneMessage, Is.True);
            Assert.That(invokeNotReadyMessage, Is.True);
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
            client.sceneManager.SetClientReady();
            Assert.That(sceneManager.Ready);
            Assert.That(client.Connection.IsReady);
        }

        [Test]
        public void ReadyTwiceTest()
        {
            sceneManager.SetClientReady();

            Assert.Throws<InvalidOperationException>(() =>
            {
                sceneManager.SetClientReady();
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

        [UnityTest]
        public IEnumerator ChangeSceneAdditiveLoadTest() => RunAsync(async () =>
        {
            server.sceneManager.ChangeServerScene("testScene", SceneOperation.LoadAdditive);

            await WaitFor(() => SceneManager.GetSceneByName("testScene") != null);

            Assert.That(SceneManager.GetSceneByName("testScene"), Is.Not.Null);
        });

        [Test]
        public void ClientNoHandlersInHostMode()
        {
            Assert.DoesNotThrow(() => { server.SendToAll(new SceneMessage()); }); 
        }
    }

    public class NetworkSceneManagerNonHostTests : ClientServerSetup<MockComponent>
    {
        AssetBundle bundle;

        public override void ExtraSetup()
        {
            bundle = AssetBundle.LoadFromFile("Assets/Mirror/Tests/Runtime/TestScene/testscene");
        }

        public override void ExtraTearDown()
        {
            bundle.Unload(true);
        }

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
            client.sceneManager.SetClientReady();
            client.sceneManager.ClientNotReadyMessage(null, new NotReadyMessage());

            Assert.That(client.sceneManager.Ready, Is.False);
            func1.Received(1).Invoke(Arg.Any<INetworkConnection>());
        }

        [UnityTest]
        public IEnumerator ClientSceneMessageInvokeTest() => RunAsync(async () =>
        {
            UnityAction<string, SceneOperation> func1 = Substitute.For<UnityAction<string, SceneOperation>>();
            client.sceneManager.ClientChangeScene.AddListener(func1);
            client.sceneManager.ClientSceneMessage(null, new SceneMessage() { sceneName = "testScene" });

            await WaitFor(() => client.sceneManager.NetworkSceneName.Equals("testScene"));

            func1.Received(1).Invoke(Arg.Any<string>(), Arg.Any<SceneOperation>());
        });

        [Test]
        public void NetworkSceneNameStringEmptyTest()
        {
            Assert.That(server.sceneManager.NetworkSceneName.Equals(string.Empty));
        }
    }
}
