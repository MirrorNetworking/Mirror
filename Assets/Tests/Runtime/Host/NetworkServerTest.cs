using System;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mirror.Tests.Host
{

    [TestFixture]
    public class NetworkServerTest : HostSetup<MockComponent>
    {
        [Test]
        public void MaxConnectionsTest()
        {
            var secondGO = new GameObject();
            NetworkClient secondClient = secondGO.AddComponent<NetworkClient>();
            Transport secondTestTransport = secondGO.AddComponent<LoopbackTransport>();

            secondClient.Transport = secondTestTransport;

            secondClient.ConnectAsync("localhost").Forget();

            Assert.That(server.connections, Has.Count.EqualTo(1));

            Object.Destroy(secondGO);
        }

        [Test]
        public void LocalClientActiveTest()
        {
            Assert.That(server.LocalClientActive, Is.True);
        }

        [Test]
        public void SetLocalConnectionExceptionTest()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                server.SetLocalConnection(null, null);
            });
        }
    }
}
