using NUnit.Framework;
using System.Collections;
using UnityEngine;

namespace JamesFrowen.BitPacking.Tests
{
    public class FloatPackerTests
    {
        private const int BufferSize = 1000;

        static IEnumerable CompressesAndDecompressesCases()
        {
            yield return new TestCaseData(1269679f, 0.1005143f, 558430.4f);
        }

        [Test]
        [TestCaseSource(nameof(CompressesAndDecompressesCases))]
        public void PackAndUnpack(float max, float percision, float inValue)
        {
            var packer = new FloatPacker(0, max, percision);

            var writer = new BitWriter(BufferSize);
            packer.Pack(writer, inValue);
            writer.Flush();

            var reader = new BitReader(writer.ToArraySegment());
            var outValue = packer.Unpack(reader);


            Assert.That(outValue, Is.Not.NaN, "x was NaN");

            Assert.That(outValue, Is.EqualTo(inValue).Within(percision * 2), $"value off by {Mathf.Abs(inValue - outValue)}");
        }
    }
}
