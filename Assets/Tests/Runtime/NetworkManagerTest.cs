using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using static Mirror.Tests.AsyncUtil;

namespace Mirror.Tests
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
        public IEnumerator IsNetworkActiveStopTest() => RunAsync(async () =>
        {
            manager.server.Disconnect();

            await WaitFor(() => !client.Active);

            Assert.That(server.Active, Is.False);
            Assert.That(client.Active, Is.False);
            Assert.That(manager.IsNetworkActive, Is.False);
        });

        [UnityTest]
        public IEnumerator StopClientTest() => RunAsync(async () =>
        {
            manager.client.Disconnect();

            await WaitFor(() => !client.Active);
        });

        [Test]
        public void StartHostException()
        {
            manager.client = null;
            Assert.Throws<InvalidOperationException>(() =>
            {
                manager.server.StartHost(manager.client).GetAwaiter().GetResult();
            });
        }
    }
}
