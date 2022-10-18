using System;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class UtilsTests
    {
        [Test]
        public void GetTrueRandomUInt()
        {
            uint first = Utils.GetTrueRandomUInt();
            uint second = Utils.GetTrueRandomUInt();
            Assert.That(first, !Is.EqualTo(second));
        }

        [Test]
        public void Pow()
        {
            Assert.That(Utils.Pow(2, 0), Is.EqualTo(1));
            Assert.That(Utils.Pow(2, 1), Is.EqualTo(2));
            Assert.That(Utils.Pow(2, 2), Is.EqualTo(4));
            Assert.That(Utils.Pow(2, 3), Is.EqualTo(8));
            Assert.That(Utils.Pow(2, 4), Is.EqualTo(16));
        }

        [Test]
        public void RoundBitsToFullBytes()
        {
            // special case:
            // 1 bit requires 1 byte to store it.
            // 0 bit however requires 0 byte.
            Assert.That(Utils.RoundBitsToFullBytes(0), Is.EqualTo(0));

            // 1 byte
            for (int i = 1; i <= 8; ++i)
                Assert.That(Utils.RoundBitsToFullBytes(i), Is.EqualTo(1));

            // 2 bytes
            for (int i = 9; i <= 16; ++i)
                Assert.That(Utils.RoundBitsToFullBytes(i), Is.EqualTo(2));

            // 3 bytes
            for (int i = 17; i <= 24; ++i)
                Assert.That(Utils.RoundBitsToFullBytes(i), Is.EqualTo(3));

            // 4 bytes
            for (int i = 25; i <= 32; ++i)
                Assert.That(Utils.RoundBitsToFullBytes(i), Is.EqualTo(4));

            // 5 bytes
            for (int i = 33; i <= 40; ++i)
                Assert.That(Utils.RoundBitsToFullBytes(i), Is.EqualTo(5));

            // 6 bytes
            for (int i = 41; i <= 48; ++i)
                Assert.That(Utils.RoundBitsToFullBytes(i), Is.EqualTo(6));

            // 7 bytes
            for (int i = 49; i <= 56; ++i)
                Assert.That(Utils.RoundBitsToFullBytes(i), Is.EqualTo(7));

            // 8 bytes
            for (int i = 57; i <= 64; ++i)
                Assert.That(Utils.RoundBitsToFullBytes(i), Is.EqualTo(8));
        }

        [Test]
        public void BitsRequired()
        {
            // min > max should never work
            Assert.Throws<ArgumentOutOfRangeException>(() => Utils.BitsRequired(2, 1));

            // 2..2 is only ever 1 value = 2, so 0 bits needed
            Assert.That(Utils.BitsRequired(2, 2), Is.EqualTo(0));

            // 3..4 are 2 values, so 1 bit
            Assert.That(Utils.BitsRequired(3, 4), Is.EqualTo(1));

            // 0..7 are 8 values, so 3 bits
            Assert.That(Utils.BitsRequired(0, 7), Is.EqualTo(3));

            // 4..7 are 4 values, so 2 bits
            Assert.That(Utils.BitsRequired(4, 7), Is.EqualTo(2));

            // 0..255 are 256 values, so 8 bits
            Assert.That(Utils.BitsRequired(0, 255), Is.EqualTo(8));

            // 128..255 are 128 values, so 7 bits
            Assert.That(Utils.BitsRequired(128, 255), Is.EqualTo(7));

            // ushort should always fit into 2 bytes
            Assert.That(Utils.BitsRequired(0, ushort.MaxValue), Is.EqualTo(16));

            // uint should always fit into 4 bytes
            Assert.That(Utils.BitsRequired(0, uint.MaxValue), Is.EqualTo(32));

            // ulong should always fit into 8 bytes
            // this is the maximum range that ever fits into an ulong.
            // a lot of things could go wrong, e.g. if we +1 to that range
            // without being careful.
            // THIS TEST IS HUGELY IMPORTANT.
            Assert.That(Utils.BitsRequired(0, ulong.MaxValue), Is.EqualTo(64));

            // since we hardcoded the values, let's test for 1..63 shifted bits
            // just to be sure that all of the hard coded values are correct!
            for (int bits = 1; bits < 64; ++bits)
            {
                // 1 bits => bitsMax = 1
                // 2 bits => bitsMax = 2
                // 3 bits => bitsMax = 4
                // etc.
                // so check 0..bitsMax-1
                ulong bitsMax = 1ul << bits;
                //UnityEngine.Debug.Log($"testing bitsMax={bitsMax-1} => bits={bits}");
                Assert.That(Utils.BitsRequired(0, bitsMax - 1), Is.EqualTo(bits));
            }
        }

        [Test]
        public void IsPointInScreen()
        {
            int width = Screen.width;
            int height = Screen.height;
            Assert.That(Utils.IsPointInScreen(new Vector2(-1, -1)), Is.False);

            Assert.That(Utils.IsPointInScreen(new Vector2(0, 0)), Is.True);
            Assert.That(Utils.IsPointInScreen(new Vector2(width / 2, height / 2)), Is.True);

            Assert.That(Utils.IsPointInScreen(new Vector2(width, height / 2)), Is.False);
            Assert.That(Utils.IsPointInScreen(new Vector2(width / 2, height)), Is.False);
            Assert.That(Utils.IsPointInScreen(new Vector2(width + 1, height + 1)), Is.False);
        }

        [Test]
        public void PrettyBytes()
        {
            // bytes
            Assert.That(Utils.PrettyBytes(0), Is.EqualTo("0 B"));
            Assert.That(Utils.PrettyBytes(512), Is.EqualTo("512 B"));
            Assert.That(Utils.PrettyBytes(1023), Is.EqualTo("1023 B"));

            // kilobytes
            Assert.That(Utils.PrettyBytes(1024), Is.EqualTo($"{1.0:F2} KB"));
            Assert.That(Utils.PrettyBytes(1024 + 512), Is.EqualTo($"{1.5:F2} KB"));
            Assert.That(Utils.PrettyBytes(2048), Is.EqualTo($"{2.0:F2} KB"));

            // megabytes
            Assert.That(Utils.PrettyBytes(1024 * 1024), Is.EqualTo($"{1.0:F2} MB"));
            Assert.That(Utils.PrettyBytes(1024 * (1024 + 512)), Is.EqualTo($"{1.5:F2} MB"));
            Assert.That(Utils.PrettyBytes(1024 * 1024 * 2), Is.EqualTo($"{2.0:F2} MB"));

            // gigabytes
            Assert.That(Utils.PrettyBytes(1024L * 1024L * 1024L), Is.EqualTo($"{1.0:F2} GB"));
            Assert.That(Utils.PrettyBytes(1024L * 1024L * (1024L + 512L)), Is.EqualTo($"{1.5:F2} GB"));
            Assert.That(Utils.PrettyBytes(1024L * 1024L * 1024L * 2L), Is.EqualTo($"{2.0:F2} GB"));
        }

        [Test]
        public void PrettySeconds()
        {
            Assert.That(Utils.PrettySeconds(0), Is.EqualTo("0s"));
            Assert.That(Utils.PrettySeconds(0.5f), Is.EqualTo("0.5s"));
            Assert.That(Utils.PrettySeconds(1), Is.EqualTo("1s"));
            Assert.That(Utils.PrettySeconds(1.5f), Is.EqualTo("1.5s"));
            Assert.That(Utils.PrettySeconds(65), Is.EqualTo("1m 5s"));
            Assert.That(Utils.PrettySeconds(60 * 61 + 5), Is.EqualTo("1h 1m 5s"));
            Assert.That(Utils.PrettySeconds(24 * 60 * 60 + 60 * 61 + 5), Is.EqualTo("1d 1h 1m 5s"));
        }
    }
}
