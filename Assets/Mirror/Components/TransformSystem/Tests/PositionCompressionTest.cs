using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.TransformSyncing.Tests
{
    public class PositionCompressionTests
    {
        static IEnumerable CompressesAndDecompressesCases()
        {
            yield return new TestCaseData(new Vector3(0, 0, 0), new Vector3(100, 100, 100), 0.01f, new Vector3(0, 0, 0));
            yield return new TestCaseData(new Vector3(0, 0, 0), new Vector3(100, 100, 100), 0.01f, new Vector3(20, 20, 20));
            yield return new TestCaseData(new Vector3(0, 0, 0), new Vector3(100, 100, 100), 0.01f, new Vector3(50, 50, 50));
            yield return new TestCaseData(new Vector3(0, 0, 0), new Vector3(100, 100, 100), 0.01f, new Vector3(100, 100, 100));
        }

        [Test]
        [TestCaseSource(nameof(CompressesAndDecompressesCases))]
        public void CompressesAndDecompresses(Vector3 min, Vector3 max, float precision, Vector3 inValue)
        {
            PositionCompression compressor = new PositionCompression(min, max, precision);


            NetworkWriter writer = new NetworkWriter();
            compressor.Compress(writer, inValue);
            NetworkReader reader = new NetworkReader(writer.ToArraySegment());
            Vector3 outValue = compressor.Decompress(reader);

            string debugMessage = $"in{inValue} out{outValue}";
            Assert.That(outValue.x, Is.EqualTo(inValue.x).Within(precision), debugMessage);
            Assert.That(outValue.y, Is.EqualTo(inValue.y).Within(precision), debugMessage);
            Assert.That(outValue.z, Is.EqualTo(inValue.z).Within(precision), debugMessage);
        }

        [Test]
        [TestCaseSource(nameof(CompressesAndDecompressesCases))]
        public void WriteHasCorrectLength(Vector3 min, Vector3 max, float precision, Vector3 inValue)
        {
            PositionCompression compressor = new PositionCompression(min, max, precision);
            int writeCount = Mathf.CeilToInt(compressor.bitCount / 8f);

            NetworkWriter writer = new NetworkWriter();
            compressor.Compress(writer, inValue);

            Assert.That(writer.Length, Is.EqualTo(writeCount));
        }

        [Test]
        [TestCaseSource(nameof(CompressesAndDecompressesCases))]
        public void ReadHasCorrectLength(Vector3 min, Vector3 max, float precision, Vector3 inValue)
        {
            PositionCompression compressor = new PositionCompression(min, max, precision);
            int readCount = Mathf.CeilToInt(compressor.bitCount / 8f);

            NetworkWriter writer = new NetworkWriter();
            compressor.Compress(writer, inValue);
            NetworkReader reader = new NetworkReader(writer.ToArraySegment());
            _ = compressor.Decompress(reader);

            Assert.That(reader.Position, Is.EqualTo(readCount));
        }


        static IEnumerable CompressesAndDecompressesCasesRepeat()
        {
            yield return new TestCaseData(new Vector3(0, 0, 0), new Vector3(100, 100, 100), 0.01f);
            yield return new TestCaseData(new Vector3(0, 0, 0), new Vector3(100, 100, 100), 0.05f);
            yield return new TestCaseData(new Vector3(-100, 0, -100), new Vector3(100, 100, 100), 0.01f);
            yield return new TestCaseData(new Vector3(-100, 0, -100), new Vector3(100, 100, 100), 0.05f);
            yield return new TestCaseData(new Vector3(0, 0, 0), new Vector3(100, 100, 100), 0.1f);
            yield return new TestCaseData(new Vector3(0, 0, 0), new Vector3(100, 100, 100), 0.5f);
            yield return new TestCaseData(new Vector3(-500, -100, -500), new Vector3(500, 100, 500), 0.5f);
            yield return new TestCaseData(new Vector3(0, -20, 0), new Vector3(500, 40, 500), 0.031f);
        }

        [Test]
        [Repeat(100)]
        [TestCaseSource(nameof(CompressesAndDecompressesCasesRepeat))]
        public void CompressesAndDecompressesRepeat(Vector3 min, Vector3 max, float precision)
        {
            Vector3 inValue = new Vector3(
                Random.Range(min.x, max.x),
                Random.Range(min.y, max.y),
                Random.Range(min.z, max.z)
                );

            CompressesAndDecompresses(min, max, precision, inValue);
            WriteHasCorrectLength(min, max, precision, inValue);
            ReadHasCorrectLength(min, max, precision, inValue);
        }
    }
}
