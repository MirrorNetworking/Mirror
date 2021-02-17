using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.Runtime.TransportEnableTest
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
            // set inactive so that awake isn't called
            gameObject.SetActive(false);

            transport1 = Substitute.For<Transport>();
            transport2 = gameObject.AddComponent<MemoryTransport>();

            transport = gameObject.AddComponent<FallbackTransport>();
            transport.transports = new[] { transport1, transport2 };

            gameObject.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            GameObject.DestroyImmediate(transport.gameObject);
        }

        [Test]
        public void DisableShouldDisableAvailableTransport()
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


    public class MultiplexTransportEnableTest
    {
        MemoryTransport transport1;
        MemoryTransport transport2;
        MultiplexTransport transport;

        [SetUp]
        public void Setup()
        {
            GameObject gameObject = new GameObject();
            // set inactive so that awake isn't called
            gameObject.SetActive(false);

            transport1 = gameObject.AddComponent<MemoryTransport>();
            transport2 = gameObject.AddComponent<MemoryTransport>();

            transport = gameObject.AddComponent<MultiplexTransport>();
            transport.transports = new[] { transport1, transport2 };

            gameObject.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            GameObject.DestroyImmediate(transport.gameObject);
        }

        [Test]
        public void DisableShouldDisableAllTransports()
        {
            transport.Awake();

            // starts enabled
            Assert.That(transport1.enabled, Is.True);
            Assert.That(transport2.enabled, Is.True);

            // disabling MultiplexTransport
            transport.enabled = false;
            Assert.That(transport1.enabled, Is.False);
            Assert.That(transport2.enabled, Is.False);

            // enabling MultiplexTransport
            transport.enabled = true;
            Assert.That(transport1.enabled, Is.True);
            Assert.That(transport2.enabled, Is.True);
        }
    }

}
