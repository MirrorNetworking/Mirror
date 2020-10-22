using System;
using NUnit.Framework;

namespace Mirror.Tests
{

    [TestFixture]
    public class KcpClassTests : KcpSetup
    {
        [Test]
        public void SendExceptionTest()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                server.Send(null, 0, 0);
            });
        }

        [Test]
        public void SetMtuExceptionTest()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                server.Mtu = 0;
            });

            Assert.Throws<ArgumentException>(() =>
            {
                server.Mtu = uint.MaxValue;
            });
        }

        [Test]
        public void ReserveExceptionTest()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                server.Reserved = int.MaxValue;
            });
        }
    }
}
