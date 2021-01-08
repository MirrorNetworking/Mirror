using NUnit.Framework;
using System;
using System.Collections;
using UnityEngine;

namespace JamesFrowen.BitPacking.Tests
{
    public class PositionPackerTests
    {
        private const int BufferSize = 1000;

        static IEnumerable CompressesAndDecompressesCases()
        {
            yield return new TestCaseData(new Vector3(0, 0, 0), new Vector3(100, 100, 100), 0.01f, new Vector3(0, 0, 0));
            yield return new TestCaseData(new Vector3(0, 0, 0), new Vector3(100, 100, 100), 0.01f, new Vector3(20, 20, 20));
            yield return new TestCaseData(new Vector3(0, 0, 0), new Vector3(100, 100, 100), 0.01f, new Vector3(50, 50, 50));
            yield return new TestCaseData(new Vector3(0, 0, 0), new Vector3(100, 100, 100), 0.01f, new Vector3(100, 100, 100));
        }

        [Test]
        [TestCaseSource(nameof(CompressesAndDecompressesCases))]
        public void PackAndUnpack(Vector3 min, Vector3 max, float precision, Vector3 inValue)
        {
            var packer = new PositionPacker(min, max, precision);

            var writer = new BitWriter(BufferSize);
            packer.Pack(writer, inValue);
            writer.Flush();

            var reader = new BitReader(writer.ToArraySegment());
            var outValue = packer.Unpack(reader);

            var debugMessage = $"in{inValue} out{outValue}";
            Assert.That(outValue.x, Is.EqualTo(inValue.x).Within(precision), debugMessage);
            Assert.That(outValue.y, Is.EqualTo(inValue.y).Within(precision), debugMessage);
            Assert.That(outValue.z, Is.EqualTo(inValue.z).Within(precision), debugMessage);
        }


        [Test]
        [TestCaseSource(nameof(CompressesAndDecompressesCases))]
        public void PackHasCorrectLength(Vector3 min, Vector3 max, float precision, Vector3 inValue)
        {
            var packer = new PositionPacker(min, max, precision);
            var writeCount = Mathf.CeilToInt(packer.bitCount / 8f);

            var writer = new BitWriter(BufferSize);
            packer.Pack(writer, inValue);
            writer.Flush();

            Assert.That(writer.Length, Is.EqualTo(writeCount));
        }

        [Test]
        [TestCaseSource(nameof(CompressesAndDecompressesCases))]
        public void UnpackHasCorrectLength(Vector3 min, Vector3 max, float precision, Vector3 inValue)
        {
            var packer = new PositionPacker(min, max, precision);
            var readCount = Mathf.CeilToInt(packer.bitCount / 8f);

            var writer = new BitWriter(BufferSize);
            packer.Pack(writer, inValue);
            writer.Flush();

            var reader = new BitReader(writer.ToArraySegment());
            var outValue = packer.Unpack(reader);

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
        public void PackAndUnpackRepeat(Vector3 min, Vector3 max, float precision)
        {
            var inValue = new Vector3(
                UnityEngine.Random.Range(min.x, max.x),
                UnityEngine.Random.Range(min.y, max.y),
                UnityEngine.Random.Range(min.z, max.z)
                );

            this.PackAndUnpack(min, max, precision, inValue);
            this.PackHasCorrectLength(min, max, precision, inValue);
            this.UnpackHasCorrectLength(min, max, precision, inValue);
        }


        [Test]
        [TestCase(0u, 1u, ExpectedResult = 1)]
        [TestCase(0u, 1024u, ExpectedResult = 11)]
        [TestCase(0u, 1000u, ExpectedResult = 10)]
        [TestCase(0u, (uint)int.MaxValue, ExpectedResult = 31)]
        [TestCase(1000u, 2000u, ExpectedResult = 10)]
        public int BitCountFromRangeGivesCorrectValues(uint min, uint max)
        {
            return BitCountHelper.BitCountFromRange(min, max);
        }
        [Test]
        [TestCase(0u, 0u)]
        [TestCase(10u, 0u)]
        public void BitCountFromRangeThrowsForBadInputs(uint min, uint max)
        {
            var execption = Assert.Throws<ArgumentException>(() =>
            {
                BitCountHelper.BitCountFromRange(min, max);
            });

            Assert.That(execption, Has.Message.EqualTo($"Min:{min} is greater or equal to than Max:{max}"));
        }
    }
}
