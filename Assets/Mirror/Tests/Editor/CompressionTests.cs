using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class CompressionTests
    {
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
            Compression.CompressVarUInt(writer, ulong.MaxValue);

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
            Assert.That(Compression.DecompressVarUInt(reader), Is.EqualTo(ulong.MaxValue));

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
    }
}
