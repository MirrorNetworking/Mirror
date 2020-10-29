using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    public class NetworkSceneManagerTests : HostSetup<MockComponent>
    {
        AssetBundle bundle;

        public override void ExtraSetup()
        {
            bundle = AssetBundle.LoadFromFile("Assets/Tests/Runtime/TestScene/testscene");
        }

        public override void ExtraTearDown()
        {
            bundle.Unload(true);
        }

        [Test]
        public void FinishLoadSceneHostTest()
        {
            UnityAction<string, SceneOperation> func2 = Substitute.For<UnityAction<string, SceneOperation>>();
            UnityAction<string, SceneOperation> func3 = Substitute.For<UnityAction<string, SceneOperation>>();

            sceneManager.ServerSceneChanged.AddListener(func2);
            sceneManager.ClientSceneChanged.AddListener(func3);

            sceneManager.FinishLoadScene("test", SceneOperation.Normal);

            func2.Received(1).Invoke(Arg.Any<string>(), Arg.Any<SceneOperation>());
            func3.Received(1).Invoke(Arg.Any<string>(), Arg.Any<SceneOperation>());
        }

        int onOnServerSceneOnlyChangedCounter;
        void TestOnServerOnlySceneChangedInvoke(string sceneName, SceneOperation sceneOperation)
        {
            onOnServerSceneOnlyChangedCounter++;
        }

        [UnityTest]
        public IEnumerator FinishLoadServerOnlyTest() => UniTask.ToCoroutine(async () =>
        {
            client.Disconnect();

            await AsyncUtil.WaitUntilWithTimeout(() => !client.Active);

            sceneManager.ServerSceneChanged.AddListener(TestOnServerOnlySceneChangedInvoke);

            sceneManager.FinishLoadScene("test", SceneOperation.Normal);

            Assert.That(onOnServerSceneOnlyChangedCounter, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator ServerChangeSceneTest() => UniTask.ToCoroutine(async () =>
        {
            bool invokeClientSceneMessage = false;
            bool invokeNotReadyMessage = false;
            UnityAction<string, SceneOperation> func1 = Substitute.For<UnityAction<string, SceneOperation>>();
            client.Connection.RegisterHandler<SceneMessage>(msg => invokeClientSceneMessage = true);
            client.Connection.RegisterHandler<NotReadyMessage>(msg => invokeNotReadyMessage = true);
            sceneManager.ServerChangeScene.AddListener(func1);

            sceneManager.ChangeServerScene("Assets/Mirror/Tests/Runtime/testScene.unity");

            await AsyncUtil.WaitUntilWithTimeout(() => sceneManager.NetworkScenePath.Equals("Assets/Mirror/Tests/Runtime/testScene.unity"));

            func1.Received(1).Invoke(Arg.Any<string>(), Arg.Any<SceneOperation>());
            Assert.That(sceneManager.NetworkScenePath, Is.EqualTo("Assets/Mirror/Tests/Runtime/testScene.unity"));
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

        [UnityTest]
        public IEnumerator ChangeSceneAdditiveLoadTest() => UniTask.ToCoroutine(async () =>
        {
            sceneManager.ChangeServerScene("Assets/Mirror/Tests/Runtime/testScene.unity", SceneOperation.LoadAdditive);

            await AsyncUtil.WaitUntilWithTimeout(() => SceneManager.GetSceneByName("testScene") != null);

            Assert.That(SceneManager.GetSceneByName("testScene"), Is.Not.Null);
        });
    }

    public class NetworkSceneManagerNonHostTests : ClientServerSetup<MockComponent>
    {
        AssetBundle bundle;

        public override void ExtraSetup()
        {
            bundle = AssetBundle.LoadFromFile("Assets/Tests/Runtime/TestScene/testscene");
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
        public void FinishLoadSceneTest()
        {
            UnityAction<string, SceneOperation> func2 = Substitute.For<UnityAction<string, SceneOperation>>();
            clientSceneManager.ClientSceneChanged.AddListener(func2);
            clientSceneManager.FinishLoadScene("Assets/Mirror/Tests/Runtime/testScene.unity", SceneOperation.Normal);

            func2.Received(1).Invoke(Arg.Any<string>(), Arg.Any<SceneOperation>());
        }

        [UnityTest]
        public IEnumerator ClientOfflineSceneException() => UniTask.ToCoroutine(async () =>
        {
            client.Disconnect();

            await AsyncUtil.WaitUntilWithTimeout(() => !client.Active);

            Assert.Throws<InvalidOperationException>(() =>
            {
                clientSceneManager.ClientSceneMessage(null, new SceneMessage());
            });
        });

        [UnityTest]
        public IEnumerator ClientSceneMessageInvokeTest() => UniTask.ToCoroutine(async () =>
        {
            UnityAction<string, SceneOperation> func1 = Substitute.For<UnityAction<string, SceneOperation>>();
            clientSceneManager.ClientChangeScene.AddListener(func1);
            clientSceneManager.ClientSceneMessage(null, new SceneMessage { scenePath = "Assets/Mirror/Tests/Runtime/testScene.unity" });

            await AsyncUtil.WaitUntilWithTimeout(() => clientSceneManager.asyncOperation != null);

            clientSceneManager.ClientSceneReadyMessage(connectionToServer, new SceneReadyMessage());

            await AsyncUtil.WaitUntilWithTimeout(() => clientSceneManager.asyncOperation.isDone);

            Assert.That(clientSceneManager.NetworkScenePath, Is.EqualTo("Assets/Mirror/Tests/Runtime/testScene.unity"));

            func1.Received(1).Invoke(Arg.Any<string>(), Arg.Any<SceneOperation>());
        });

        [Test]
        public void NetworkSceneNameStringValueTest()
        {
            Assert.That(clientSceneManager.NetworkScenePath.Equals(SceneManager.GetActiveScene().path));
        }

        [Test]
        public void AsyncOperationInitStateTest()
        {
            Assert.That(clientSceneManager.asyncOperation, Is.Null);
        }

        [Test]
        public void ServerChangeSceneTest()
        {
            UnityAction<string, SceneOperation> func1 = Substitute.For<UnityAction<string, SceneOperation>>();
            serverSceneManager.ServerChangeScene.AddListener(func1);
            serverSceneManager.OnServerChangeScene("test", SceneOperation.Normal);
            func1.Received(1).Invoke(Arg.Any<string>(), Arg.Any<SceneOperation>());
        }

        [Test]
        public void ServerSceneChangedTest()
        {
            UnityAction<string, SceneOperation> func1 = Substitute.For<UnityAction<string, SceneOperation>>();
            serverSceneManager.ServerSceneChanged.AddListener(func1);
            serverSceneManager.OnServerSceneChanged("test", SceneOperation.Normal);
            func1.Received(1).Invoke(Arg.Any<string>(), Arg.Any<SceneOperation>());
        }
    }
}
