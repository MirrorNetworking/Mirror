using System;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.Tools
{
    public class CompressionTests
    {
        [Test]
        public void ScaleToLong_Scalar()
        {
            // origin
            Assert.True(Compression.ScaleToLong(0, 0.1f, out long value));
            Assert.That(value, Is.EqualTo(0));

            // 10m far
            Assert.True(Compression.ScaleToLong(10.5f, 0.1f, out value));
            Assert.That(value, Is.EqualTo(105));

            // 100m far
            Assert.True(Compression.ScaleToLong(100.5f, 0.1f, out value));
            Assert.That(value, Is.EqualTo(1005));

            // 10km
            Assert.True(Compression.ScaleToLong(10_000.5f, 0.1f, out value));
            Assert.That(value, Is.EqualTo(100005));

            // 1000 km
            Assert.True(Compression.ScaleToLong(1_000_000.5f, 0.1f, out value));
            Assert.That(value, Is.EqualTo(10000005));

            // negative
            Assert.True(Compression.ScaleToLong(-1_000_000.5f, 0.1f, out value));
            Assert.That(value, Is.EqualTo(-10000005));
        }

        // users may try to 'disable' the scaling by setting precision = 0.
        // this would cause null division. need to detect and throw so the user
        // knows it needs immediate fixing.
        [Test]
        public void ScaleToLong_Precision_0()
        {
            Assert.Throws<DivideByZeroException>(() =>
            {
                Compression.ScaleToLong(10.5f, 0, out _);
            });
        }

        [Test]
        public void ScaleToLong_Scalar_OutOfRange()
        {
            float precision = 0.1f;
            float largest = long.MaxValue / 0.1f;
            float smallest = long.MinValue / 0.1f;

            // larger than long.max should clamp to max and return false
            Assert.False(Compression.ScaleToLong(largest + 1, precision, out long value));
            Assert.That(value, Is.EqualTo(long.MaxValue));

            // smaller than long.min should clamp to min and return false
            Assert.False(Compression.ScaleToLong(smallest - 1, precision, out value));
            Assert.That(value, Is.EqualTo(long.MinValue));
        }

        [Test]
        public void ScaleToLong_Vector3()
        {
            // 0, positive, negative
            Assert.True(Compression.ScaleToLong(new Vector3(0, 10.5f, -100.5f), 0.1f, out long x, out long y, out long z));
            Assert.That(x, Is.EqualTo(0));
            Assert.That(y, Is.EqualTo(105));
            Assert.That(z, Is.EqualTo(-1005));
        }

        [Test]
        public void ScaleToLong_Vector3_OutOfRange()
        {
            float precision = 0.1f;
            float largest = long.MaxValue / 0.1f;
            float smallest = long.MinValue / 0.1f;

            // 0, largest, smallest
            Assert.False(Compression.ScaleToLong(new Vector3(0, largest, smallest), precision, out long x, out long y, out long z));
            Assert.That(x, Is.EqualTo(0));
            Assert.That(y, Is.EqualTo(long.MaxValue));
            Assert.That(z, Is.EqualTo(long.MinValue));
        }

        [Test]
        public void ScaleToLong_Quaternion()
        {
            // 0, positive, negative
            Assert.True(Compression.ScaleToLong(new Quaternion(0, 10.5f, -100.5f, -10.5f), 0.1f, out long x, out long y, out long z, out long w));
            Assert.That(x, Is.EqualTo(0));
            Assert.That(y, Is.EqualTo(105));
            Assert.That(z, Is.EqualTo(-1005));
            Assert.That(w, Is.EqualTo(-105));
        }

        [Test]
        public void ScaleToLong_Quaternion_OutOfRange()
        {
            float precision = 0.1f;
            float largest = long.MaxValue / 0.1f;
            float smallest = long.MinValue / 0.1f;

            // 0, largest, smallest
            Assert.False(Compression.ScaleToLong(new Quaternion(0, largest, smallest, 0), precision, out long x, out long y, out long z, out long w));
            Assert.That(x, Is.EqualTo(0));
            Assert.That(y, Is.EqualTo(long.MaxValue));
            Assert.That(z, Is.EqualTo(long.MinValue));
            Assert.That(w, Is.EqualTo(0));
        }

        [Test]
        public void ScaleToFloat()
        {
            // origin
            Assert.That(Compression.ScaleToFloat(0, 0.1f), Is.EqualTo(0));

            // 10m far
            Assert.That(Compression.ScaleToFloat(105, 0.1f), Is.EqualTo(10.5f));

            // 100m far
            Assert.That(Compression.ScaleToFloat(1005, 0.1f), Is.EqualTo(100.5f));

            // 10km
            Assert.That(Compression.ScaleToFloat(100005, 0.1f), Is.EqualTo(10_000.5f));

            // 1000 km
            Assert.That(Compression.ScaleToFloat(10000005, 0.1f), Is.EqualTo(1_000_000.5f));

            // negative
            Assert.That(Compression.ScaleToFloat(-10000005, 0.1f), Is.EqualTo(-1_000_000.5f));
        }

        // users may try to 'disable' the scaling by setting precision = 0.
        // this would cause null division. need to detect and throw so the user
        // knows it needs immediate fixing.
        [Test]
        public void ScaleToFloat_Precision_0()
        {
            Assert.Throws<DivideByZeroException>(() =>
            {
                Compression.ScaleToFloat(105, 0);
            });
        }

        [Test]
        public void ScaleToFloat_Vector3()
        {
            // 0, positive, negative
            Vector3 v = Compression.ScaleToFloat(0, 105, -1005, 0.1f);
            Assert.That(v.x, Is.EqualTo(0));
            Assert.That(v.y, Is.EqualTo(10.5f));
            Assert.That(v.z, Is.EqualTo(-100.5f));
        }

        [Test]
        public void ScaleToFloat_Quaternion()
        {
            // 0, positive, negative
            Quaternion q = Compression.ScaleToFloat(0, 105, -1005, -105, 0.1f);
            Assert.That(q.x, Is.EqualTo(0));
            Assert.That(q.y, Is.EqualTo(10.5f));
            Assert.That(q.z, Is.EqualTo(-100.5f));
            Assert.That(q.w, Is.EqualTo(-10.5f));
        }

        [Test]
        public void LargestAbsoluteComponentIndex()
        {
            // positive value & xyw smallest
            Vector4 value = new Vector4(1, 3, 4, 2);
            int index = Compression.LargestAbsoluteComponentIndex(value, out float largest, out Vector3 withoutLargest);
            Assert.That(index, Is.EqualTo(2));
            Assert.That(largest, Is.EqualTo(Mathf.Abs(value.z)));
            Assert.That(withoutLargest, Is.EqualTo(new Vector3(value.x, value.y, value.w)));

            // negative value should use abs & xzw smallest
            value = new Vector4(1, -5, 4, 0);
            index = Compression.LargestAbsoluteComponentIndex(value, out largest, out withoutLargest);
            Assert.That(index, Is.EqualTo(1));
            Assert.That(largest, Is.EqualTo(Mathf.Abs(value.y)));
            Assert.That(withoutLargest, Is.EqualTo(new Vector3(value.x, value.z, value.w)));

            // positive value & yzw smallest
            value = new Vector4(5, 2, 3, 4);
            index = Compression.LargestAbsoluteComponentIndex(value, out largest, out withoutLargest);
            Assert.That(index, Is.EqualTo(0));
            Assert.That(largest, Is.EqualTo(Mathf.Abs(value.x)));
            Assert.That(withoutLargest, Is.EqualTo(new Vector3(value.y, value.z, value.w)));

            // test to guarantee it uses 'abs' for first value
            // to reproduce https://github.com/vis2k/Mirror/issues/2674
            // IF all values are properly 'abs', THEN first one should be largest
            value = new Vector4(-3, 0, 1, 2);
            index = Compression.LargestAbsoluteComponentIndex(value, out largest, out withoutLargest);
            Assert.That(index, Is.EqualTo(0));
            Assert.That(largest, Is.EqualTo(Mathf.Abs(value.x)));
            Assert.That(withoutLargest, Is.EqualTo(new Vector3(value.y, value.z, value.w)));
        }

        [Test, Ignore("Enable when needed.")]
        public void LargestAbsoluteComponentIndexBenchmark()
        {
            Vector4 value = new Vector4(1, 2, 3, 4);
            for (int i = 0; i < 100000; ++i)
                Compression.LargestAbsoluteComponentIndex(value, out float _, out Vector3 _);
        }

        [Test]
        public void ScaleFloatToUShort()
        {
            Assert.That(Compression.ScaleFloatToUShort(-1f, -1f, 1f, ushort.MinValue, ushort.MaxValue), Is.EqualTo(0));
            Assert.That(Compression.ScaleFloatToUShort(0f, -1f, 1f, ushort.MinValue, ushort.MaxValue), Is.EqualTo(32767));
            Assert.That(Compression.ScaleFloatToUShort(0.5f, -1f, 1f, ushort.MinValue, ushort.MaxValue), Is.EqualTo(49151));
            Assert.That(Compression.ScaleFloatToUShort(1f, -1f, 1f, ushort.MinValue, ushort.MaxValue), Is.EqualTo(65535));
        }

        [Test]
        public void ScaleUShortToFloat()
        {
            Assert.That(Compression.ScaleUShortToFloat(0, ushort.MinValue, ushort.MaxValue, -1, 1), Is.EqualTo(-1).Within(0.0001f));
            Assert.That(Compression.ScaleUShortToFloat(32767, ushort.MinValue, ushort.MaxValue, -1, 1), Is.EqualTo(-0f).Within(0.0001f));
            Assert.That(Compression.ScaleUShortToFloat(49151, ushort.MinValue, ushort.MaxValue, -1, 1), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(Compression.ScaleUShortToFloat(65535, ushort.MinValue, ushort.MaxValue, -1, 1), Is.EqualTo(1).Within(0.0001f));
        }

        [Test]
        public void CompressAndDecompressQuaternion()
        {
            // we need a normalized value
            Quaternion value = new Quaternion(1, 3, 4, 2).normalized;

            // compress
            uint data = Compression.CompressQuaternion(value);
            Assert.That(data, Is.EqualTo(0xA83E2F07));

            // decompress
            Quaternion decompressed = Compression.DecompressQuaternion(data);
            Assert.That(decompressed.x, Is.EqualTo(value.x).Within(0.005f));
            Assert.That(decompressed.y, Is.EqualTo(value.y).Within(0.005f));
            Assert.That(decompressed.z, Is.EqualTo(value.z).Within(0.005f));
            Assert.That(decompressed.w, Is.EqualTo(value.w).Within(0.005f));
        }

        // iterate all [0..360] euler angles for x, y, z
        // to make sure it all works and we missed nothing.
        [Test]
        public void CompressAndDecompressQuaternion_Iterate_0_to_360()
        {
            // stepSize  1: 360 * 360 * 360 =  46 million  [takes 96 s]
            // stepSize  5:  72 *  72 *  72 = 373 thousand [takes 700 ms]
            // stepSize 10:  36 *  36 *  36 =  46 thousand [takes 100 ms]
            //
            // => 10 is enough. 700ms accumulates in hours of time waited over
            //    the years..
            const int stepSize = 10;

            for (int x = 0; x <= 360; x += stepSize)
            {
                for (int y = 0; y <= 360; y += stepSize)
                {
                    for (int z = 0; z <= 360; z += stepSize)
                    {
                        // we need a normalized value
                        Quaternion value = Quaternion.Euler(x, y, z).normalized;

                        // compress
                        uint data = Compression.CompressQuaternion(value);

                        // decompress
                        Quaternion decompressed = Compression.DecompressQuaternion(data);

                        // compare them. Quaternion.Angle is easiest to get the angle
                        // between them. using .eulerAngles would give 0, 90, 360 which is
                        // hard to compare.
                        float angle = Quaternion.Angle(value, decompressed);
                        // 1 degree tolerance
                        Assert.That(Mathf.Abs(angle), Is.LessThanOrEqualTo(1));
                    }
                }
            }
        }

        // someone mentioned issues with 90 degree euler becoming -90 degree
        [Test]
        public void CompressAndDecompressQuaternion_90DegreeEuler()
        {
            // we need a normalized value
            Quaternion value = Quaternion.Euler(0, 90, 0).normalized;

            // compress
            uint data = Compression.CompressQuaternion(value);

            // decompress
            Quaternion decompressed = Compression.DecompressQuaternion(data);

            // compare them. Quaternion.Angle is easiest to get the angle
            // between them. using .eulerAngles would give 0, 90, 360 which is
            // hard to compare.
            Debug.Log($"euler={decompressed.eulerAngles}");
            float angle = Quaternion.Angle(value, decompressed);
            // 1 degree tolerance
            Assert.That(Mathf.Abs(angle), Is.LessThanOrEqualTo(1));
        }

        // test for issue https://github.com/vis2k/Mirror/issues/2674
        [Test]
        public void CompressAndDecompressQuaternion_2674()
        {
            // we need a normalized value
            Quaternion value = Quaternion.Euler(338.850037f, 170.609955f, 182.979996f).normalized;
            Debug.Log($"original={value.eulerAngles}");

            // compress
            uint data = Compression.CompressQuaternion(value);

            // decompress
            Quaternion decompressed = Compression.DecompressQuaternion(data);

            // compare them. Quaternion.Angle is easiest to get the angle
            // between them. using .eulerAngles would give 0, 90, 360 which is
            // hard to compare.

            //  (51.6, 355.5, 348.1)
            Debug.Log($"euler={decompressed.eulerAngles}");
            float angle = Quaternion.Angle(value, decompressed);
            // 1 degree tolerance
            Assert.That(Mathf.Abs(angle), Is.LessThanOrEqualTo(1));
        }

        // client sending invalid data should still produce valid quaternions to
        // avoid any possible bugs on server
        [Test]
        public void DecompressQuaternionInvalidData()
        {
            // decompress
            // 0xFFFFFFFF will decompress to (0.7, 0.7, 0.7, NaN)
            Quaternion decompressed = Compression.DecompressQuaternion(0xFFFFFFFF);
            Assert.That(decompressed, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void VarInt()
        {
            NetworkWriter writer = new NetworkWriter();

            Compression.CompressVarInt(writer, long.MinValue);
            Compression.CompressVarInt(writer, -72057594037927935);
            Compression.CompressVarInt(writer, -281474976710655);
            Compression.CompressVarInt(writer, -1099511627775);
            Compression.CompressVarInt(writer, -4294967295);
            Compression.CompressVarInt(writer, -16777219);
            Compression.CompressVarInt(writer, -16777210);
            Compression.CompressVarInt(writer, -67821);
            Compression.CompressVarInt(writer, -2284);
            Compression.CompressVarInt(writer, -234);
            Compression.CompressVarInt(writer, 0);
            Compression.CompressVarInt(writer, 234);
            Compression.CompressVarInt(writer, 2284);
            Compression.CompressVarInt(writer, 67821);
            Compression.CompressVarInt(writer, 16777210);
            Compression.CompressVarInt(writer, 16777219);
            Compression.CompressVarInt(writer, 4294967295);
            Compression.CompressVarInt(writer, 1099511627775);
            Compression.CompressVarInt(writer, 281474976710655);
            Compression.CompressVarInt(writer, 72057594037927935);
            Compression.CompressVarInt(writer, long.MaxValue);

            NetworkReader reader = new NetworkReader(writer.ToArray());

            Assert.That(Compression.DecompressVarInt(reader), Is.EqualTo(long.MinValue));
            Assert.That(Compression.DecompressVarInt(reader), Is.EqualTo(-72057594037927935));
            Assert.That(Compression.DecompressVarInt(reader), Is.EqualTo(-281474976710655));
            Assert.That(Compression.DecompressVarInt(reader), Is.EqualTo(-1099511627775));
            Assert.That(Compression.DecompressVarInt(reader), Is.EqualTo(-4294967295));
            Assert.That(Compression.DecompressVarInt(reader), Is.EqualTo(-16777219));
            Assert.That(Compression.DecompressVarInt(reader), Is.EqualTo(-16777210));
            Assert.That(Compression.DecompressVarInt(reader), Is.EqualTo(-67821));
            Assert.That(Compression.DecompressVarInt(reader), Is.EqualTo(-2284));
            Assert.That(Compression.DecompressVarInt(reader), Is.EqualTo(-234));
            Assert.That(Compression.DecompressVarInt(reader), Is.EqualTo(0));
            Assert.That(Compression.DecompressVarInt(reader), Is.EqualTo(234));
            Assert.That(Compression.DecompressVarInt(reader), Is.EqualTo(2284));
            Assert.That(Compression.DecompressVarInt(reader), Is.EqualTo(67821));
            Assert.That(Compression.DecompressVarInt(reader), Is.EqualTo(16777210));
            Assert.That(Compression.DecompressVarInt(reader), Is.EqualTo(16777219));
            Assert.That(Compression.DecompressVarInt(reader), Is.EqualTo(4294967295));
            Assert.That(Compression.DecompressVarInt(reader), Is.EqualTo(1099511627775));
            Assert.That(Compression.DecompressVarInt(reader), Is.EqualTo(281474976710655));
            Assert.That(Compression.DecompressVarInt(reader), Is.EqualTo(72057594037927935));
            Assert.That(Compression.DecompressVarInt(reader), Is.EqualTo(long.MaxValue));
        }

        [Test]
        public void VarUInt()
        {
            NetworkWriter writer = new NetworkWriter();
            Compression.CompressVarUInt(writer, 0);
            Compression.CompressVarUInt(writer, 234);
            Compression.CompressVarUInt(writer, 2284);
            Compression.CompressVarUInt(writer, 67821);
            Compression.CompressVarUInt(writer, 16777210);
            Compression.CompressVarUInt(writer, 16777219);
            Compression.CompressVarUInt(writer, 4294967295);
            Compression.CompressVarUInt(writer, 1099511627775);
            Compression.CompressVarUInt(writer, 281474976710655);
            Compression.CompressVarUInt(writer, 72057594037927935);
            Compression.CompressVarUInt(writer, ulong.MaxValue / 10);
            Compression.CompressVarUInt(writer, ulong.MaxValue / 8);
            Compression.CompressVarUInt(writer, ulong.MaxValue / 6);
            Compression.CompressVarUInt(writer, ulong.MaxValue / 4);
            Compression.CompressVarUInt(writer, ulong.MaxValue / 2);
            Compression.CompressVarUInt(writer, 0x01020304A1A2A3A4); // every byte different (!)
            Compression.CompressVarUInt(writer, ulong.MaxValue);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            Assert.That(Compression.DecompressVarUInt(reader), Is.EqualTo(0));
            Assert.That(Compression.DecompressVarUInt(reader), Is.EqualTo(234));
            Assert.That(Compression.DecompressVarUInt(reader), Is.EqualTo(2284));
            Assert.That(Compression.DecompressVarUInt(reader), Is.EqualTo(67821));
            Assert.That(Compression.DecompressVarUInt(reader), Is.EqualTo(16777210));
            Assert.That(Compression.DecompressVarUInt(reader), Is.EqualTo(16777219));
            Assert.That(Compression.DecompressVarUInt(reader), Is.EqualTo(4294967295));
            Assert.That(Compression.DecompressVarUInt(reader), Is.EqualTo(1099511627775));
            Assert.That(Compression.DecompressVarUInt(reader), Is.EqualTo(281474976710655));
            Assert.That(Compression.DecompressVarUInt(reader), Is.EqualTo(72057594037927935));
            Assert.That(Compression.DecompressVarUInt(reader), Is.EqualTo(ulong.MaxValue / 10));
            Assert.That(Compression.DecompressVarUInt(reader), Is.EqualTo(ulong.MaxValue / 8));
            Assert.That(Compression.DecompressVarUInt(reader), Is.EqualTo(ulong.MaxValue / 6));
            Assert.That(Compression.DecompressVarUInt(reader), Is.EqualTo(ulong.MaxValue / 4));
            Assert.That(Compression.DecompressVarUInt(reader), Is.EqualTo(ulong.MaxValue / 2));
            Assert.That(Compression.DecompressVarUInt(reader), Is.EqualTo(0x01020304A1A2A3A4)); // every byte different (!)
            Assert.That(Compression.DecompressVarUInt(reader), Is.EqualTo(ulong.MaxValue));
        }

        [Test]
        [TestCase(0ul)]
        [TestCase(234ul)]
        [TestCase(2284ul)]
        [TestCase(67821ul)]
        [TestCase(16777210ul)]
        [TestCase(16777219ul)]
        [TestCase(4294967295ul)]
        [TestCase(1099511627775ul)]
        [TestCase(281474976710655ul)]
        [TestCase(72057594037927935ul)]
        [TestCase(ulong.MaxValue)]
        public void VarUIntSize(ulong number)
        {
            NetworkWriter writer = new NetworkWriter();
            Compression.CompressVarUInt(writer, number);
            Assert.That(writer.Position, Is.EqualTo(Compression.VarUIntSize(number)));
        }

        [Test]
        [TestCase(long.MinValue)]
        [TestCase(-72057594037927935)]
        [TestCase(-281474976710655)]
        [TestCase(-1099511627775)]
        [TestCase(-4294967295)]
        [TestCase(-16777219)]
        [TestCase(-16777210)]
        [TestCase(-67821)]
        [TestCase(-2284)]
        [TestCase(-234)]
        [TestCase(0)]
        [TestCase(234)]
        [TestCase(2284)]
        [TestCase(67821)]
        [TestCase(16777210)]
        [TestCase(16777219)]
        [TestCase(4294967295)]
        [TestCase(1099511627775)]
        [TestCase(281474976710655)]
        [TestCase(72057594037927935)]
        [TestCase(long.MaxValue)]
        public void VarIntSize(long number)
        {
            NetworkWriter writer = new NetworkWriter();
            Compression.CompressVarInt(writer, number);
            Assert.That(writer.Position, Is.EqualTo(Compression.VarIntSize(number)));
        }
    }
}
