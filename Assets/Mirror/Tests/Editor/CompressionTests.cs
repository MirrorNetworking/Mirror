using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class CompressionTests
    {
        [Test]
        [Repeat(1000)]
        public void QuaternionCompression()
        {
            Quaternion expected = Random.rotation;

            uint compressed = Compression.CompressQuaternion(expected);

            Quaternion decompressed = Compression.DecompressQuaternion(compressed);

            float angle = Quaternion.Angle(expected, decompressed);
            if (angle > 1.2f)
                Debug.LogWarning($"{expected}\t{angle}");

            Assert.That(angle, Is.LessThan(1.3f));

            // decompressed should be almost the same
            Assert.That(Mathf.Abs(Quaternion.Dot(expected, decompressed)), Is.GreaterThan(1 - 0.001));
        }

        // User supplied data that gave bad results
        // Quaternion.Euler(338.850037f,170.609955f,182.979996f)
        // Quaternion.Euler(338.850006f,230.809998f,182.979996f)

        [Test]
        public void QuaternionCompression2()
        {
            Quaternion expected = Quaternion.Euler(338.850037f, 170.609955f, 182.979996f);

            uint compressed = Compression.CompressQuaternion(expected);

            Quaternion decompressed = Compression.DecompressQuaternion(compressed);

            float angle = Quaternion.Angle(expected, decompressed);
            Debug.Log($"{expected}\t{angle}");

            Assert.That(angle, Is.LessThan(1f));

            // decompressed should be almost the same
            Assert.That(Mathf.Abs(Quaternion.Dot(expected, decompressed)), Is.GreaterThan(1 - 0.001));
        }

        [Test]
        public void QuaternionCompression3()
        {
            Quaternion expected = Quaternion.Euler(338.850006f, 230.809998f, 182.979996f);

            uint compressed = Compression.CompressQuaternion(expected);

            Quaternion decompressed = Compression.DecompressQuaternion(compressed);

            float angle = Quaternion.Angle(expected, decompressed);
            Debug.Log($"{expected}\t{angle}");

            Assert.That(angle, Is.LessThan(1f));

            // decompressed should be almost the same
            Assert.That(Mathf.Abs(Quaternion.Dot(expected, decompressed)), Is.GreaterThan(1 - 0.001));
        }

        [Test]
        public void Compress90Degrees()
        {
            uint compressed = Compression.CompressQuaternion(Quaternion.Euler(0, 90, 0));

            Quaternion decompressed = Compression.DecompressQuaternion(compressed);

            Vector3 euler = decompressed.eulerAngles;
            Assert.That(euler.y, Is.EqualTo(90).Within(0.005f));
        }

        //[Test]
        //public void LargestAbsoluteComponentIndex()
        //{
        //    // positive value & xyw smallest
        //    Vector4 value = new Vector4(1, 3, 4, 2);
        //    int index = Compression.LargestAbsoluteComponentIndex(value, out float largest, out Vector3 withoutLargest);
        //    Assert.That(index, Is.EqualTo(2));
        //    Assert.That(largest, Is.EqualTo(Mathf.Abs(value.z)));
        //    Assert.That(withoutLargest, Is.EqualTo(new Vector3(value.x, value.y, value.w)));

        //    // negative value should use abs & xzw smallest
        //    value = new Vector4(1, -5, 4, 0);
        //    index = Compression.LargestAbsoluteComponentIndex(value, out largest, out withoutLargest);
        //    Assert.That(index, Is.EqualTo(1));
        //    Assert.That(largest, Is.EqualTo(Mathf.Abs(value.y)));
        //    Assert.That(withoutLargest, Is.EqualTo(new Vector3(value.x, value.z, value.w)));

        //    // positive value & yzw smallest
        //    value = new Vector4(5, 2, 3, 4);
        //    index = Compression.LargestAbsoluteComponentIndex(value, out largest, out withoutLargest);
        //    Assert.That(index, Is.EqualTo(0));
        //    Assert.That(largest, Is.EqualTo(Mathf.Abs(value.x)));
        //    Assert.That(withoutLargest, Is.EqualTo(new Vector3(value.y, value.z, value.w)));
        //}

        //[Test]
        //public void LargestAbsoluteComponentIndexBenchmark()
        //{
        //    Vector4 value = new Vector4(1, 2, 3, 4);
        //    for (int i = 0; i < 100000; ++i)
        //        Compression.LargestAbsoluteComponentIndex(value, out float _, out Vector3 _);
        //}

        //[Test]
        //public void ScaleToUint()
        //{
        //    Assert.That(Compression.ScaleToUint(-1f), Is.EqualTo(0));
        //    Assert.That(Compression.ScaleToUint(0f), Is.EqualTo(32767));
        //    Assert.That(Compression.ScaleToUint(0.5f), Is.EqualTo(49151));
        //    Assert.That(Compression.ScaleToUint(1f), Is.EqualTo(65535));
        //}

        //[Test]
        //public void ScaleToFloat()
        //{
        //    Assert.That(Compression.ScaleToFloat(0), Is.EqualTo(-1).Within(0.0001f));
        //    Assert.That(Compression.ScaleToFloat(32767), Is.EqualTo(-0f).Within(0.0001f));
        //    Assert.That(Compression.ScaleToFloat(49151), Is.EqualTo(0.5f).Within(0.0001f));
        //    Assert.That(Compression.ScaleToFloat(65535), Is.EqualTo(1).Within(0.0001f));
        //}

        [Test]
        public void CompressAndDecompressQuaternion()
        {
            // we need a normalized value
            Quaternion value = new Quaternion(1, 3, 4, 2).normalized;

            // compress
            uint data = Compression.CompressQuaternion(value);
            Assert.That(data, Is.EqualTo(0x88463108));

            // decompress
            Quaternion decompressed = Compression.DecompressQuaternion(data);
            Assert.That(decompressed.x, Is.EqualTo(value.x).Within(0.005f));
            Assert.That(decompressed.y, Is.EqualTo(value.y).Within(0.005f));
            Assert.That(decompressed.z, Is.EqualTo(value.z).Within(0.005f));
            Assert.That(decompressed.w, Is.EqualTo(value.w).Within(0.005f));
        }

        //// someone mentioned issues with 90 degree euler becoming -90 degree
        //[Test]
        //public void CompressAndDecompressQuaternion_90DegreeEuler()
        //{
        //    // we need a normalized value
        //    Quaternion value = Quaternion.Euler(0, 90, 0).normalized;

        //    // compress
        //    uint data = Compression.CompressQuaternion(value);

        //    // decompress
        //    Quaternion decompressed = Compression.DecompressQuaternion(data);

        //    // compare them. Quaternion.Angle is easiest to get the angle
        //    // between them. using .eulerAngles would give 0, 90, 360 which is
        //    // hard to compare.
        //    Debug.Log("euler=" + decompressed.eulerAngles);
        //    float angle = Quaternion.Angle(value, decompressed);
        //    // 1 degree tolerance
        //    Assert.That(Mathf.Abs(angle), Is.LessThanOrEqualTo(1));
        //}

        // client sending invalid data should still produce valid quaternions to
        // avoid any possible bugs on server
        [Test, Ignore("TODO")]
        public void DecompressQuaternionInvalidData()
        {
            // decompress
            // 0xFFFFFFFF will decompress to (0.7, 0.7, 0.7, NaN)
            Quaternion decompressed = Compression.DecompressQuaternion(0xFFFFFFFF);
            Assert.That(decompressed, Is.EqualTo(Quaternion.identity));
        }

        // test for issue https://github.com/vis2k/Mirror/issues/2674
        [Test, Ignore("TODO")]
        public void CompressAndDecompressQuaternion_2674()
        {
            // we need a normalized value
            Quaternion value = Quaternion.Euler(338.850037f, 170.609955f, 182.979996f).normalized;
            Debug.Log("immediate=" + value.eulerAngles);

            // compress
            uint data = Compression.CompressQuaternion(value);

            // decompress
            Quaternion decompressed = Compression.DecompressQuaternion(data);

            // compare them. Quaternion.Angle is easiest to get the angle
            // between them. using .eulerAngles would give 0, 90, 360 which is
            // hard to compare.

            //  (51.6, 355.5, 348.1)
            Debug.Log("euler=" + decompressed.eulerAngles);
            float angle = Quaternion.Angle(value, decompressed);
            // 1 degree tolerance
            Assert.That(Mathf.Abs(angle), Is.LessThanOrEqualTo(1));
        }
    }
}
