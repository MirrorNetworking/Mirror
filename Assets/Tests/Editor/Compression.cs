using Mirror;
using NUnit.Framework;
using UnityEngine;

namespace Tests
{
    public class CompressionTests
    {

        [Test]
        [Repeat(100)]
        public void QuaternionCompression()
        {
            Quaternion expected = Random.rotation;

            uint compressed = Compression.Compress(expected);

            Quaternion decompressed = Compression.Decompress(compressed);

            // decompressed should be almost the same
            Assert.That(Mathf.Abs(Quaternion.Dot(expected, decompressed)), Is.GreaterThan(1 - 0.001));
        }
    }
}
