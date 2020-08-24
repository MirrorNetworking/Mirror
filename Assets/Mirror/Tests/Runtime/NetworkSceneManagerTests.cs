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
            UnityAction<string, SceneOperation> func2 = Substitute.For<UnityAction<string, SceneOperation>>();
            UnityAction<string, SceneOperation> func3 = Substitute.For<UnityAction<string, SceneOperation>>();

            client.Authenticated.AddListener(func1);
            sceneManager.ServerSceneChanged.AddListener(func2);
            sceneManager.ClientSceneChanged.AddListener(func3);

            sceneManager.FinishLoadScene("test", SceneOperation.Normal);

            func1.Received(1).Invoke(Arg.Any<INetworkConnection>());
            func2.Received(1).Invoke(Arg.Any<string>(), Arg.Any<SceneOperation>());
            func3.Received(1).Invoke(Arg.Any<string>(), Arg.Any<SceneOperation>());
        }

        int onOnServerSceneOnlyChangedCounter;
        void TestOnServerOnlySceneChangedInvoke(string sceneName, SceneOperation sceneOperation)
        {
            onOnServerSceneOnlyChangedCounter++;
        }

        [UnityTest]
        public IEnumerator FinishLoadServerOnlyTest() => RunAsync(async () =>
        {
            client.Disconnect();

            await WaitFor(() => !client.Active);

            sceneManager.ServerSceneChanged.AddListener(TestOnServerOnlySceneChangedInvoke);

            sceneManager.FinishLoadScene("test", SceneOperation.Normal);

            Assert.That(onOnServerSceneOnlyChangedCounter, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator ServerChangeSceneTest() => RunAsync(async () =>
        {
            bool invokeClientSceneMessage = false;
            bool invokeNotReadyMessage = false;
            UnityAction<string, SceneOperation> func1 = Substitute.For<UnityAction<string, SceneOperation>>();
            client.Connection.RegisterHandler<SceneMessage>(msg => invokeClientSceneMessage = true);
            client.Connection.RegisterHandler<NotReadyMessage>(msg => invokeNotReadyMessage = true);
            sceneManager.ServerChangeScene.AddListener(func1);

            sceneManager.ChangeServerScene("testScene");

            await WaitFor(() => invokeClientSceneMessage && invokeNotReadyMessage);

            func1.Received(1).Invoke(Arg.Any<string>(), Arg.Any<SceneOperation>());
            Assert.That(sceneManager.NetworkSceneName, Is.EqualTo("testScene"));
            Assert.That(invokeClientSceneMessage, Is.True);
            Assert.That(invokeNotReadyMessage, Is.True);
        });

        [Test]
        public void ChangeServerSceneExceptionTest()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                sceneManager.ChangeServerScene(string.Empty);
            });
        }

        [Test]
        public void ReadyTest()
        {
            sceneManager.SetClientReady();
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
            UnityAction<string, SceneOperation> func1 = Substitute.For<UnityAction<string, SceneOperation>>();
            sceneManager.ClientSceneChanged.AddListener(func1);
            sceneManager.OnClientSceneChanged("test", SceneOperation.Normal);
            func1.Received(1).Invoke(Arg.Any<string>(), Arg.Any<SceneOperation>());
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
            sceneManager.ChangeServerScene("testScene", SceneOperation.LoadAdditive);

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
                clientSceneManager.ClientSceneMessage(null, new SceneMessage());
            });
        }

        [Test]
        public void FinishLoadSceneHostTest()
        {
            UnityAction<INetworkConnection> func1 = Substitute.For<UnityAction<INetworkConnection>>();
            UnityAction<string, SceneOperation> func2 = Substitute.For<UnityAction<string, SceneOperation>>();

            client.Authenticated.AddListener(func1);
            clientSceneManager.ClientSceneChanged.AddListener(func2);

            clientSceneManager.FinishLoadScene("test", SceneOperation.Normal);

            func1.Received(1).Invoke(Arg.Any<INetworkConnection>());
            func2.Received(1).Invoke(Arg.Any<string>(), Arg.Any<SceneOperation>());
        }

        [UnityTest]
        public IEnumerator ClientOfflineSceneException() => RunAsync(async () =>
        {
            client.Disconnect();

            await WaitFor(() => !client.Active);

            Assert.Throws<InvalidOperationException>(() =>
            {
                clientSceneManager.ClientSceneMessage(null, new SceneMessage());
            });
        });

        [Test]
        public void ClientNotReady()
        {
            UnityAction<INetworkConnection> func1 = Substitute.For<UnityAction<INetworkConnection>>();
            clientSceneManager.ClientNotReady.AddListener(func1);
            clientSceneManager.SetClientReady();
            clientSceneManager.ClientNotReadyMessage(null, new NotReadyMessage());

            Assert.That(client.Connection.IsReady, Is.False);
            func1.Received(1).Invoke(Arg.Any<INetworkConnection>());
        }

        [UnityTest]
        public IEnumerator ClientSceneMessageInvokeTest() => RunAsync(async () =>
        {
            UnityAction<string, SceneOperation> func1 = Substitute.For<UnityAction<string, SceneOperation>>();
            clientSceneManager.ClientChangeScene.AddListener(func1);
            clientSceneManager.ClientSceneMessage(null, new SceneMessage { sceneName = "testScene" });

            await WaitFor(() => clientSceneManager.NetworkSceneName.Equals("testScene"));

            func1.Received(1).Invoke(Arg.Any<string>(), Arg.Any<SceneOperation>());
        });

        [Test]
        public void NetworkSceneNameStringEmptyTest()
        {
            Assert.That(clientSceneManager.NetworkSceneName.Equals(string.Empty));
        }

        [Test]
        public void AsyncOperationInitStateTest()
        {
            Assert.That(clientSceneManager.asyncOperation, Is.Null);
        }

        [Test]
        public void AsyncOperationStateTest()
        {
            clientSceneManager.ClientSceneMessage(null, new SceneMessage { sceneName = "testScene" });

            Assert.That(clientSceneManager.asyncOperation, Is.Not.Null);
            Assert.That(clientSceneManager.asyncOperation.allowSceneActivation, Is.False);
        }

        [Test]
        public void ClientSceneReadyMessageTest()
        {
            clientSceneManager.ClientSceneMessage(null, new SceneMessage { sceneName = "testScene" });
            clientSceneManager.ClientSceneReadyMessage(null, new SceneReadyMessage());

            Assert.That(clientSceneManager.asyncOperation.allowSceneActivation, Is.True);
        }
    }
}
