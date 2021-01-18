using System.Collections;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Mirror.Tests.Host
{
    [TestFixture]
    public class NetworkClientTest : HostSetup<MockComponent>
    {
        [Test]
        public void IsConnectedTest()
        {
            Assert.That(client.IsConnected);
        }

        [Test]
        public void ConnectionTest()
        {
            Assert.That(client.Connection != null);
        }

        [Test]
        public void GetNewConnectionTest()
        {
            Assert.That(client.GetNewConnection(Substitute.For<IConnection>()), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ClientDisconnectTest() => UniTask.ToCoroutine(async () =>
        {
            client.Disconnect();

            await AsyncUtil.WaitUntilWithTimeout(() => client.connectState == ConnectState.Disconnected);
            await AsyncUtil.WaitUntilWithTimeout(() => !client.Active);
        });

        [Test]
        public void ConnectionClearHandlersTest()
        {
            NetworkConnection clientConn = client.Connection as NetworkConnection;

            Assert.That(clientConn.messageHandlers.Count > 0);

            clientConn.ClearHandlers();

            Assert.That(clientConn.messageHandlers.Count == 0);
        }
    }
}
