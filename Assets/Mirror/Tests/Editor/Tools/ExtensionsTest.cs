using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NUnit.Framework;

namespace Mirror.Tests.Tools
{
    public class ExtensionsTest
    {
        // supposed to return same result on all platforms
        [Test]
        public void GetStableHashHode()
        {
            Assert.That("".GetStableHashCode(), Is.EqualTo(-2128831035));
            Assert.That("Test".GetStableHashCode(), Is.EqualTo(805092869));
        }

        [Test]
        public void CopyToList()
        {
            List<int> source = new List<int> {1, 2, 3};
            List<int> destination = new List<int>();
            source.CopyTo(destination);
            Assert.That(destination.SequenceEqual(source), Is.True);
        }

        [Test]
        public void ArraySegment_ToHexString()
        {
            ArraySegment<byte> segment = new ArraySegment<byte>(new byte[] {0xAA, 0xBB, 0xCC});
            Assert.That(segment.ToHexString(), Is.EqualTo("AA-BB-CC"));
        }

        [Test]
        public void IPEndPoint_PrettyAddress()
        {
            // IPv4
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Loopback, 1337);
            Assert.That(endPoint.PrettyAddress(), Is.EqualTo("127.0.0.1"));

            endPoint = new IPEndPoint(IPAddress.Parse("255.255.255.255"), 1337);
            Assert.That(endPoint.PrettyAddress(), Is.EqualTo("255.255.255.255"));

            // IPv4 mapped to IPv6 should automatically display as IPv4 for readability
            endPoint = new IPEndPoint(IPAddress.Loopback.MapToIPv6(), 1337);
            Assert.That(endPoint.PrettyAddress(), Is.EqualTo("127.0.0.1"));

            // IPv6
            endPoint = new IPEndPoint(IPAddress.IPv6Loopback, 1337);
            Assert.That(endPoint.PrettyAddress(), Is.EqualTo("::1"));
        }
    }
}
