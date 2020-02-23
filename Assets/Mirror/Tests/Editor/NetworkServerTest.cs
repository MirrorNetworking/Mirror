using System;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    [TestFixture]
    public class NetworkServerTest
    {
        [SetUp]
        public void SetUp()
        {
            Transport.activeTransport = Substitute.For<Transport>();
        }

        [TearDown]
        public void TearDown()
        {
            Transport.activeTransport = null;
        }

        [Test]
        public void IsActiveTest()
        {
            Assert.That(NetworkServer.active, Is.False);
            NetworkServer.Listen(1);
            Assert.That(NetworkServer.active, Is.True);
            NetworkServer.Shutdown();
            Assert.That(NetworkServer.active, Is.False);
        }
    }
}
