using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Mirror.Tests.Tools
{
    public class ExtensionsTest
    {
        // supposed to return same result on all platforms
        [Test]
        public void GetStableHashHode()
        {
            Assert.That("".GetStableHashCode(), Is.EqualTo(23));
            Assert.That("Test".GetStableHashCode(), Is.EqualTo(23844169));
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
    }
}
