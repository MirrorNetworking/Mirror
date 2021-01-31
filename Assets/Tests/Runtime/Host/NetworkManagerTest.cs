using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Mirror.Tests.Host
{
    [TestFixture]
    public class NetworkManagerTest : HostSetup<MockComponent>
    {
        [Test]
        public void IsNetworkActiveTest()
        {
            Assert.That(manager.IsNetworkActive, Is.True);
        }

        [UnityTest]
        public IEnumerator IsNetworkActiveStopTest() => UniTask.ToCoroutine(async () =>
        {
            manager.Server.Disconnect();

            await AsyncUtil.WaitUntilWithTimeout(() => !client.Active);

            Assert.That(server.Active, Is.False);
            Assert.That(client.Active, Is.False);
            Assert.That(manager.IsNetworkActive, Is.False);
        });

        [UnityTest]
        public IEnumerator StopClientTest() => UniTask.ToCoroutine(async () =>
        {
            manager.Client.Disconnect();

            await AsyncUtil.WaitUntilWithTimeout(() => !client.Active);
        });

        [Test]
        public void StartHostException()
        {
            manager.Client = null;
            Assert.Throws<InvalidOperationException>(() =>
            {
                manager.Server.StartHost(manager.Client).GetAwaiter().GetResult();
            });
        }
    }
}
