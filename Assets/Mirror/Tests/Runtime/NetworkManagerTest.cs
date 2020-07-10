using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using static Mirror.Tests.AsyncUtil;

namespace Mirror.Tests
{
    [TestFixture]
    public class NetworkManagerTest : HostSetup<MockComponent>
    {
        [Test]
        public void VariableTest()
        {
            Assert.That(manager.startOnHeadless, Is.False);
            Assert.That(manager.serverTickRate, Is.EqualTo(30));
            Assert.That(manager.server.MaxConnections, Is.EqualTo(4));
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

            await Task.Delay(1);

            Assert.That(server.Active, Is.False);
            Assert.That(client.Active, Is.False);
            Assert.That(manager.IsNetworkActive, Is.False);
        });

        [UnityTest]
        public IEnumerator StopClientTest() => RunAsync(async () =>
        {
            manager.StopClient();

            await Task.Delay(1);

            Assert.That(client.Active, Is.False);
        });
    }
}
