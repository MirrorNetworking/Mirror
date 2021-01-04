using System.Collections;
using System.Collections.Generic;
using Mirror;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using UnityEngine;

namespace JamesFrowen.BitPacking.Tests
{
    public class QuaternionPackerTests
    {
        static IEnumerable ReturnsCorrectIndexCases()
        {
            List<float> values = new List<float>() { 0.1f, 0.2f, 0.3f, 0.4f };
            // abcd are index
            // testing all permutation, index can only be used once
            for (int a = 0; a < 4; a++)
            {
                for (int b = 0; b < 4; b++)
                {
                    if (b == a) { continue; }

                    for (int c = 0; c < 4; c++)
                    {
                        if (c == a || c == b) { continue; }

                        for (int d = 0; d < 4; d++)
                        {
                            if (d == a || d == b || d == c) { continue; }

                            int largest = 0;
                            // index 3 is the largest, 
                            if (a == 3) { largest = 0; }
                            if (b == 3) { largest = 1; }
                            if (c == 3) { largest = 2; }
                            if (d == 3) { largest = 3; }
                            yield return new TestCaseData(values[a], values[b], values[c], values[d])
                                .Returns(largest);
                        }
                    }
                }
            }
        }

        [Test]
        [TestCaseSource(nameof(ReturnsCorrectIndexCases))]
        public int ReturnsCorrectIndex(float x, float y, float z, float w)
        {
            QuaternionPacker.FindLargestIndex(x, y, z, w, out int index, out float largest);
            return index;
        }


        static IEnumerable CompressesAndDecompressesCases()
        {
            for (int i = 8; i < 12; i++)
            {
                yield return new TestCaseData(i, Quaternion.identity);
                yield return new TestCaseData(i, Quaternion.Euler(25, 30, 0));
                yield return new TestCaseData(i, Quaternion.Euler(-50, 30, 90));
                yield return new TestCaseData(i, Quaternion.Euler(90, 90, 180));
                yield return new TestCaseData(i, Quaternion.Euler(-20, 0, 45));
                yield return new TestCaseData(i, Quaternion.Euler(80, 30, -45));
            }
        }

        [Test]
        [TestCaseSource(nameof(CompressesAndDecompressesCases))]
        public void PackAndUnpack(int bits, Quaternion inValue)
        {
            // this isnt exact precision but it should be greater than real precision
            float precision = 2f / ((1 << bits) - 1) * 1.5f;

            QuaternionPacker packer = new QuaternionPacker(bits);

            NetworkWriter netWriter = new NetworkWriter();
            BitWriter writer = new BitWriter(netWriter);
            packer.Pack(writer, inValue);
            writer.Flush();

            NetworkReader netReader = new NetworkReader(netWriter.ToArraySegment());
            BitReader reader = new BitReader(netReader);
            Quaternion outValue = packer.Unpack(reader);


            Assert.That(outValue.x, Is.Not.NaN, "x was NaN");
            Assert.That(outValue.y, Is.Not.NaN, "y was NaN");
            Assert.That(outValue.z, Is.Not.NaN, "z was NaN");
            Assert.That(outValue.w, Is.Not.NaN, "w was NaN");

            QuaternionPacker.FindLargestIndex(outValue.x, outValue.y, outValue.z, outValue.w, out int _, out float larggest);
            float sign = Mathf.Sign(larggest);
            // flip sign of A if largest is is negative
            // Q == (-Q)

            Assert.That(outValue.x, IsUnSignedEqualWithIn(inValue.x), $"x off by {Mathf.Abs(sign * inValue.x - outValue.x)}");
            Assert.That(outValue.y, IsUnSignedEqualWithIn(inValue.y), $"y off by {Mathf.Abs(sign * inValue.y - outValue.y)}");
            Assert.That(outValue.z, IsUnSignedEqualWithIn(inValue.z), $"z off by {Mathf.Abs(sign * inValue.z - outValue.z)}");
            Assert.That(outValue.w, IsUnSignedEqualWithIn(inValue.w), $"w off by {Mathf.Abs(sign * inValue.w - outValue.w)}");

            EqualConstraint IsUnSignedEqualWithIn(float v)
            {
                return Is.EqualTo(v).Within(precision).Or.EqualTo(sign * v).Within(precision);
            }

            Vector3 inVec = inValue * Vector3.forward;
            Vector3 outVec = outValue * Vector3.forward;

            // allow for extra precision when rotating vector
            Assert.AreEqual(inVec.x, outVec.x, precision * 2, $"vx off by {Mathf.Abs(inVec.x - outVec.x)}");
            Assert.AreEqual(inVec.y, outVec.y, precision * 2, $"vy off by {Mathf.Abs(inVec.y - outVec.y)}");
            Assert.AreEqual(inVec.z, outVec.z, precision * 2, $"vz off by {Mathf.Abs(inVec.z - outVec.z)}");

        }


        [Test]
        [Repeat(100)]
        public void PackerGivesSameValuesAsCompression()
        {
            Quaternion inValue = Random.rotation;
            QuaternionPacker packer = new QuaternionPacker(10);
            Quaternion outValuePacked = PackUnpack(inValue, packer);
            Quaternion outValueCompressed = CompressDecompress(inValue);

            Assert.That(QuaternionAlmostEqual(outValuePacked, outValueCompressed, 0.00001f),
                $"Out Values should be the same, Angle:{Quaternion.Angle(outValuePacked, outValueCompressed)}\n" +
                $"  packedFromIn: {Quaternion.Angle(inValue, outValuePacked)}\n" +
                $"  compreFromIn: {Quaternion.Angle(inValue, outValueCompressed)}\n" +
                $"  inValue: {inValue}\n" +
                $"  outValuePacked: {outValuePacked}\n" +
                $"  outValueCompressed: {outValueCompressed}\n"
                );
        }

        private static Quaternion CompressDecompress(Quaternion inValue)
        {
            NetworkWriter netWriter = new NetworkWriter();
            netWriter.WriteUInt32(Compression.CompressQuaternion(inValue));

            NetworkReader netReader = new NetworkReader(netWriter.ToArraySegment());
            return Compression.DecompressQuaternion(netReader.ReadUInt32());
        }

        private static Quaternion PackUnpack(Quaternion inValue, QuaternionPacker packer)
        {
            NetworkWriter netWriter = new NetworkWriter();
            BitWriter writer = new BitWriter(netWriter);
            packer.Pack(writer, inValue);
            writer.Flush();

            NetworkReader netReader = new NetworkReader(netWriter.ToArraySegment());
            BitReader reader = new BitReader(netReader);
            return packer.Unpack(reader);
        }


        public static bool QuaternionAlmostEqual(Quaternion actual, Quaternion expected, float precision)
        {
            return FloatAlmostEqual(actual.x, expected.x, precision)
                && FloatAlmostEqual(actual.y, expected.y, precision)
                && FloatAlmostEqual(actual.z, expected.z, precision)
                && FloatAlmostEqual(actual.w, expected.w, precision);
        }

        public static bool FloatAlmostEqual(float actual, float expected, float precision)
        {
            float minAllowed = expected - precision;
            float maxnAllowed = expected + precision;

            return minAllowed < actual && actual < maxnAllowed;
        }
    }
}
