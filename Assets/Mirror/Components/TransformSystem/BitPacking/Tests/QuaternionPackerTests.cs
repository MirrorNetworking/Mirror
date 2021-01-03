using System.Collections;
using Mirror;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using UnityEngine;

namespace JamesFrowen.BitPacking.Tests
{
    public class QuaternionPackerTests
    {
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
    }
}
