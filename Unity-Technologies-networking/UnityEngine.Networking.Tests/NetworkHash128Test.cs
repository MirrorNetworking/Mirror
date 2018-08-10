using NUnit.Framework;
using System;
using UnityEngine;

namespace UnityEngine.Networking.Tests
{
    [TestFixture]
    public class NetworkHash128Test
    {
        [Test]
        public void TestParse()
        {
            string guid = "0123456789abcdef9876543210fedcba";
            NetworkHash128 hash = NetworkHash128.Parse(guid);

            Assert.That(hash.i0, Is.EqualTo(0x01));
            Assert.That(hash.i1, Is.EqualTo(0x23));
            Assert.That(hash.i2, Is.EqualTo(0x45));
            Assert.That(hash.i3, Is.EqualTo(0x67));
            Assert.That(hash.i4, Is.EqualTo(0x89));
            Assert.That(hash.i5, Is.EqualTo(0xab));
            Assert.That(hash.i6, Is.EqualTo(0xcd));
            Assert.That(hash.i7, Is.EqualTo(0xef));
            Assert.That(hash.i8, Is.EqualTo(0x98));
            Assert.That(hash.i9, Is.EqualTo(0x76));
            Assert.That(hash.i10, Is.EqualTo(0x54));
            Assert.That(hash.i11, Is.EqualTo(0x32));
            Assert.That(hash.i12, Is.EqualTo(0x10));
            Assert.That(hash.i13, Is.EqualTo(0xfe));
            Assert.That(hash.i14, Is.EqualTo(0xdc));
            Assert.That(hash.i15, Is.EqualTo(0xba));
        }

        [Test]
        public void TestToString()
        {
            string guid = "0123456789abcdef9876543210fedcba";
            NetworkHash128 hash = NetworkHash128.Parse(guid);
            Assert.That(hash.ToString(), Is.EqualTo(guid));
        }
    }
}
