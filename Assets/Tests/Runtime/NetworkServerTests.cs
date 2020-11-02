using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    public class NetworkServerTests : ClientServerSetup<MockComponent>
    {
        WovenTestMessage message;

        public override void ExtraSetup()
        {
            message = new WovenTestMessage
            {
                IntValue = 1,
                DoubleValue = 1.0,
                StringValue = "hello"
            };

        }

        [Test]
        public void InitializeTest()
        {
            Assert.That(server.connections, Has.Count.EqualTo(1));
            Assert.That(server.Active);
            Assert.That(server.LocalClientActive, Is.False);
        }

        [Test]
        public void SendToClientOfPlayerExceptionTest()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                server.SendToClientOfPlayer<ServerRpcMessage>(null, new ServerRpcMessage());
            });
        }

        [UnityTest]
        public IEnumerator ReadyMessageSetsClientReadyTest() => UniTask.ToCoroutine(async () =>
        {
            connectionToServer.Send(new ReadyMessage());

            await AsyncUtil.WaitUntilWithTimeout(() => connectionToClient.IsReady);

            // ready?
            Assert.That(connectionToClient.IsReady, Is.True);
        });

        [UnityTest]
        public IEnumerator SendToAll() => UniTask.ToCoroutine(async () =>
        {
            bool invoked = false;

            connectionToServer.RegisterHandler<WovenTestMessage>(msg => invoked = true);

            server.SendToAll(message);

            connectionToServer.ProcessMessagesAsync().Forget();

            await AsyncUtil.WaitUntilWithTimeout(() => invoked);
        });

        [UnityTest]
        public IEnumerator SendToClientOfPlayer() => UniTask.ToCoroutine(async () =>
        {
            bool invoked = false;

            connectionToServer.RegisterHandler<WovenTestMessage>(msg => invoked = true) ;

            server.SendToClientOfPlayer(serverIdentity, message);

            connectionToServer.ProcessMessagesAsync().Forget();

            await AsyncUtil.WaitUntilWithTimeout(() => invoked);
        });

        [UnityTest]
        public IEnumerator RegisterMessage1() => UniTask.ToCoroutine(async () =>
        {
            bool invoked = false;

            connectionToClient.RegisterHandler< WovenTestMessage>(msg => invoked = true);
            connectionToServer.Send(message);

            await AsyncUtil.WaitUntilWithTimeout(() => invoked);

        });

        [UnityTest]
        public IEnumerator RegisterMessage2() => UniTask.ToCoroutine(async () =>
        {
            bool invoked = false;

            connectionToClient.RegisterHandler<WovenTestMessage>((conn, msg) => invoked = true);

            connectionToServer.Send(message);

            await AsyncUtil.WaitUntilWithTimeout(() => invoked);
        });

        [UnityTest]
        public IEnumerator UnRegisterMessage1() => UniTask.ToCoroutine(async () =>
        {
            Action<WovenTestMessage> func = Substitute.For<Action<WovenTestMessage>>();

            connectionToClient.RegisterHandler(func);
            connectionToClient.UnregisterHandler<WovenTestMessage>();

            connectionToServer.Send(message);

            await UniTask.Delay(1);

            func.Received(0).Invoke(
                Arg.Any<WovenTestMessage>());
        });

        [Test]
        public void NumPlayersTest()
        {
            Assert.That(server.NumPlayers, Is.EqualTo(1));
        }

        [Test]
        public void GetNewConnectionTest()
        {
            Assert.That(server.GetNewConnection(Substitute.For<IConnection>()), Is.Not.Null);
        }

        [Test]
        public void VariableTest()
        {
            Assert.That(server.MaxConnections, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator DisconnectStateTest() => UniTask.ToCoroutine(async () =>
        {
            server.Disconnect();

            await AsyncUtil.WaitUntilWithTimeout(() => !server.Active);
        });

        [UnityTest]
        public IEnumerator StoppedInvokeTest() => UniTask.ToCoroutine(async () =>
        {
            UnityAction func1 = Substitute.For<UnityAction>();
            server.Stopped.AddListener(func1);

            server.Disconnect();

            await AsyncUtil.WaitUntilWithTimeout(() => !server.Active);

            func1.Received(1).Invoke();
        });
    }
}
