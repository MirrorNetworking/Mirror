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
        }

        [Test]
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
    }
}
