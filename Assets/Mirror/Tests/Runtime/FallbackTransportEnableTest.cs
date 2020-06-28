using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.Runtime
{
    public class FallbackTransportEnableTest
    {
        Transport transport1;
        MemoryTransport transport2;
        FallbackTransport transport;

        [SetUp]
        public void Setup()
        {
            GameObject gameObject = new GameObject();

            transport1 = Substitute.For<Transport>();
            transport2 = gameObject.AddComponent<MemoryTransport>();

            // set inactive so that awake isnt called
            gameObject.SetActive(false);
            transport = gameObject.AddComponent<FallbackTransport>();
            transport.transports = new[] { transport1, transport2 };
            gameObject.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            GameObject.DestroyImmediate(transport.gameObject);
            GameObject.DestroyImmediate(transport2.gameObject);
        }

        [Test]
        public void DisableShouldDisableTransports()
        {
            // make transport2 the active transport
            transport1.Available().Returns(false);
            transport.Awake();

            // starts enabled
            Assert.That(transport2.enabled, Is.True);

            // disabling FallbackTransport
            transport.enabled = false;
            Assert.That(transport2.enabled, Is.False);

            // enabling FallbackTransport
            transport.enabled = true;
            Assert.That(transport2.enabled, Is.True);
        }
    }
}
