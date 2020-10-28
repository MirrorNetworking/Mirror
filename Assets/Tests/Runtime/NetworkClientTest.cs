using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using NSubstitute;
using Cysharp.Threading.Tasks;

namespace Mirror.Tests
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
        public void CurrentTest()
        {
            Assert.That(NetworkClient.Current == null);
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
    }
}
