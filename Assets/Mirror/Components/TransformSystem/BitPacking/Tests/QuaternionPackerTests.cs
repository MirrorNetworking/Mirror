using NUnit.Framework;
using NUnit.Framework.Constraints;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace JamesFrowen.BitPacking.Tests
{
    public class QuaternionPackerTests
    {
        private const int BufferSize = 1000;

        static float Precision(int bits)
        {
            // sqrt2 / range * 3
            // * 3 because largest value is caculated from smallest 3, their precision error is additive
            return 1.404f / ((1 << bits) - 1) * 3f;
        }

        static IEnumerable ReturnsCorrectIndexCases()
        {
            var values = new List<float>() { 0.1f, 0.2f, 0.3f, 0.4f };
            // abcd are index
            // testing all permutation, index can only be used once
            for (var a = 0; a < 4; a++)
            {
                for (var b = 0; b < 4; b++)
                {
                    if (b == a) { continue; }

                    for (var c = 0; c < 4; c++)
                    {
                        if (c == a || c == b) { continue; }

                        for (var d = 0; d < 4; d++)
                        {
                            if (d == a || d == b || d == c) { continue; }

                            var largest = 0;
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
            QuaternionPacker.FindLargestIndex(x, y, z, w, out var index, out var largest);
            return index;
        }


        static IEnumerable CompressesAndDecompressesCases()
        {
            for (var i = 8; i < 12; i++)
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
            var precision = Precision(bits);

            var packer = new QuaternionPacker(bits);

            var writer = new BitWriter(BufferSize);
            packer.Pack(writer, inValue);
            writer.Flush();

            var reader = new BitReader(writer.ToArraySegment());
            var outValue = packer.Unpack(reader);


            Assert.That(outValue.x, Is.Not.NaN, "x was NaN");
            Assert.That(outValue.y, Is.Not.NaN, "y was NaN");
            Assert.That(outValue.z, Is.Not.NaN, "z was NaN");
            Assert.That(outValue.w, Is.Not.NaN, "w was NaN");

            QuaternionPacker.FindLargestIndex(outValue.x, outValue.y, outValue.z, outValue.w, out var _, out var larggest);
            var sign = Mathf.Sign(larggest);
            // flip sign of A if largest is is negative
            // Q == (-Q)

            Assert.That(outValue.x, IsUnSignedEqualWithIn(inValue.x), $"x off by {Mathf.Abs(sign * inValue.x - outValue.x)}");
            Assert.That(outValue.y, IsUnSignedEqualWithIn(inValue.y), $"y off by {Mathf.Abs(sign * inValue.y - outValue.y)}");
            Assert.That(outValue.z, IsUnSignedEqualWithIn(inValue.z), $"z off by {Mathf.Abs(sign * inValue.z - outValue.z)}");
            Assert.That(outValue.w, IsUnSignedEqualWithIn(inValue.w), $"w off by {Mathf.Abs(sign * inValue.w - outValue.w)}");


            var inVec = inValue * Vector3.forward;
            var outVec = outValue * Vector3.forward;

            // allow for extra precision when rotating vector
            Assert.AreEqual(inVec.x, outVec.x, precision * 2, $"vx off by {Mathf.Abs(inVec.x - outVec.x)}");
            Assert.AreEqual(inVec.y, outVec.y, precision * 2, $"vy off by {Mathf.Abs(inVec.y - outVec.y)}");
            Assert.AreEqual(inVec.z, outVec.z, precision * 2, $"vz off by {Mathf.Abs(inVec.z - outVec.z)}");


            EqualConstraint IsUnSignedEqualWithIn(float v)
            {
                return Is.EqualTo(v).Within(precision).Or.EqualTo(sign * v).Within(precision);
            }
        }


        [Test]
        [Repeat(100)]
        [Ignore("Requires mirror")]
        public void PackerGivesSameValuesAsCompression()
        {
            var inValue = UnityEngine.Random.rotation;
            var packer = new QuaternionPacker(10);
            var outValuePacked = PackUnpack(inValue, packer);
            var outValueCompressed = CompressDecompress(inValue);

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
            throw new NotSupportedException("Requires mirror");
            //netWriter.WriteUInt32(Compression.CompressQuaternion(inValue));

            //return Compression.DecompressQuaternion(netReader.ReadUInt32());
        }

        private static Quaternion PackUnpack(Quaternion inValue, QuaternionPacker packer)
        {
            var writer = new BitWriter(BufferSize);
            packer.Pack(writer, inValue);
            writer.Flush();

            var reader = new BitReader(writer.ToArraySegment());
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
            var minAllowed = expected - precision;
            var maxnAllowed = expected + precision;

            return minAllowed < actual && actual < maxnAllowed;
        }
    }
}
