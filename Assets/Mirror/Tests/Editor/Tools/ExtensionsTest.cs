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

        [Test]
        public void SwapRemove()
        {
            List<int> list = new List<int>();

            // one entry
            list.Clear();
            list.Add(42);
            list.SwapRemove(0);
            Assert.That(list.Count, Is.EqualTo(0));

            // multiple entries - remove first
            list.Clear();
            list.Add(42);
            list.Add(43);
            list.Add(44);
            list.SwapRemove(0);
            Assert.That(list.Count, Is.EqualTo(2));
            Assert.That(list[0], Is.EqualTo(44));
            Assert.That(list[1], Is.EqualTo(43)); // order changed due to swap

            // multiple entries - remove middle
            list.Clear();
            list.Add(42);
            list.Add(43);
            list.Add(44);
            list.SwapRemove(1);
            Assert.That(list.Count, Is.EqualTo(2));
            Assert.That(list[0], Is.EqualTo(42));
            Assert.That(list[1], Is.EqualTo(44));

            // multiple entries - remove last
            list.Clear();
            list.Add(42);
            list.Add(43);
            list.Add(44);
            list.SwapRemove(2);
            Assert.That(list.Count, Is.EqualTo(2));
            Assert.That(list[0], Is.EqualTo(42));
            Assert.That(list[1], Is.EqualTo(43));
        }
    }
}
