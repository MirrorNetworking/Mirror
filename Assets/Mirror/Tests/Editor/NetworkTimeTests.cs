using System;
using NUnit.Framework;

namespace Mirror.Tests
{
    [TestFixture]
    public class NetworkTimeTests
    {
        [Test]
        public void NetworkTimeTest()
        {
            NetworkTime.Reset();
            //NetworkTime.UpdateClient();
            Assert.That(NetworkTime.PingFrequency, Is.EqualTo(2f));
            Assert.That(NetworkTime.PingWindowSize, Is.EqualTo(10));

            Assert.That(NetworkTime.rtt, Is.GreaterThanOrEqualTo(0));
            Assert.That(NetworkTime.rttSd, Is.GreaterThanOrEqualTo(0));
            Assert.That(NetworkTime.rttVar, Is.GreaterThanOrEqualTo(0));
            Assert.That(NetworkTime.time, Is.GreaterThanOrEqualTo(0));
            Assert.That(NetworkTime.timeSd, Is.GreaterThanOrEqualTo(0));
            Assert.That(NetworkTime.timeVar, Is.GreaterThanOrEqualTo(0));
            Assert.That(NetworkTime.offset, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void NetworkPongMessageTest()
        {
            NetworkPongMessage message = new NetworkPongMessage
            {
                clientTime = DateTime.Now.ToOADate(),
                serverTime = DateTime.Now.AddSeconds(1).ToOADate(),
            };

            NetworkTime.OnClientPong(message);
        }
    }
}
