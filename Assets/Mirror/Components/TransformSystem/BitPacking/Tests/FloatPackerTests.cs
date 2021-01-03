using System.Collections;
using Mirror;
using NUnit.Framework;
using UnityEngine;

namespace JamesFrowen.BitPacking.Tests
{
    public class FloatPackerTests
    {
        static IEnumerable CompressesAndDecompressesCases()
        {
            yield return new TestCaseData(1269679f, 0.1005143f, 558430.4f);
        }

        [Test]
        [TestCaseSource(nameof(CompressesAndDecompressesCases))]
        public void PackAndUnpack(float max, float percision, float inValue)
        {
            FloatPacker packer = new FloatPacker(0, max, percision);

            NetworkWriter netWriter = new NetworkWriter();
            BitWriter writer = new BitWriter(netWriter);
            packer.Pack(writer, inValue);
            writer.Flush();

            NetworkReader netReader = new NetworkReader(netWriter.ToArraySegment());
            BitReader reader = new BitReader(netReader);
            float outValue = packer.Unpack(reader);


            Assert.That(outValue, Is.Not.NaN, "x was NaN");

            Assert.That(outValue, Is.EqualTo(inValue).Within(percision * 2), $"value off by {Mathf.Abs(inValue - outValue)}");
        }
    }
}
