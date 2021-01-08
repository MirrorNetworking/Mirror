using System;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

using System.Linq;
using Cysharp.Threading.Tasks;

namespace Mirror.Tests
{

    struct WovenTestMessage
    {
        public int IntValue;
        public string StringValue;
        public double DoubleValue;
    }

    [TestFixture]
    public class NetworkServerTest : HostSetup<MockComponent>
    {
        [Test]
        public void MaxConnectionsTest()
        {
            GameObject secondGO = new GameObject();
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
