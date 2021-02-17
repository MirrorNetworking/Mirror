using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class CompressionQuaternionTest
    {
        // worse case where xyzw all equal error in largest is ~1.732 times greater than error in smallest 3
        // High/Low Precision fails when xyzw all equal,
        internal const float AllowedPrecision = 0.00138f;

        [Test]
        [TestCaseSource(nameof(QuaternionTestCases))]
        public void QuaternionCompressIsWithinPrecision(Quaternion inRot)
        {
            uint packed = Compression.CompressQuaternion(inRot);

            Quaternion outRot = Compression.DecompressQuaternion(packed);

            Assert.That(outRot.x, Is.Not.NaN, "x was NaN");
            Assert.That(outRot.y, Is.Not.NaN, "y was NaN");
            Assert.That(outRot.z, Is.Not.NaN, "z was NaN");
            Assert.That(outRot.w, Is.Not.NaN, "w was NaN");

            int largest = Compression.FindLargestIndex(inRot);
            float sign = Mathf.Sign(inRot[largest]);
            // flip sign of A if largest is negative
            // Q == (-Q)

            Assert.AreEqual(sign * inRot.x, outRot.x, AllowedPrecision, $"x off by {Mathf.Abs(sign * inRot.x - outRot.x)}");
            Assert.AreEqual(sign * inRot.y, outRot.y, AllowedPrecision, $"y off by {Mathf.Abs(sign * inRot.y - outRot.y)}");
            Assert.AreEqual(sign * inRot.z, outRot.z, AllowedPrecision, $"z off by {Mathf.Abs(sign * inRot.z - outRot.z)}");
            Assert.AreEqual(sign * inRot.w, outRot.w, AllowedPrecision, $"w off by {Mathf.Abs(sign * inRot.w - outRot.w)}");
        }

        static IEnumerable QuaternionTestCases
        {
            get
            {
                yield return new TestCaseData(Quaternion.identity);
                yield return new TestCaseData(new Quaternion(1, 0, 0, 0));
                yield return new TestCaseData(new Quaternion(0, 1, 0, 0));
                yield return new TestCaseData(new Quaternion(0, 0, 1, 0));

                yield return new TestCaseData(new Quaternion(1, 1, 0, 0).normalized);
                yield return new TestCaseData(new Quaternion(0, 1, 1, 0).normalized);
                yield return new TestCaseData(new Quaternion(0, 1, 1, 0).normalized);
                yield return new TestCaseData(new Quaternion(0, 0, 1, 1).normalized);

                yield return new TestCaseData(new Quaternion(1, 1, 1, 0).normalized);
                yield return new TestCaseData(new Quaternion(1, 1, 0, 1).normalized);
                yield return new TestCaseData(new Quaternion(1, 0, 1, 1).normalized);
                yield return new TestCaseData(new Quaternion(0, 1, 1, 1).normalized);

                yield return new TestCaseData(new Quaternion(1, 1, 1, 1).normalized);

                yield return new TestCaseData(new Quaternion(-1, 0, 0, 0));
                yield return new TestCaseData(new Quaternion(0, -1, 0, 0));
                yield return new TestCaseData(new Quaternion(0, 0, -1, 0));
                yield return new TestCaseData(new Quaternion(0, 0, 0, -1));

                yield return new TestCaseData(new Quaternion(-1, -1, 0, 0).normalized);
                yield return new TestCaseData(new Quaternion(0, -1, -1, 0).normalized);
                yield return new TestCaseData(new Quaternion(0, -1, -1, 0).normalized);
                yield return new TestCaseData(new Quaternion(0, 0, -1, -1).normalized);

                yield return new TestCaseData(new Quaternion(-1, -1, -1, 0).normalized);
                yield return new TestCaseData(new Quaternion(-1, -1, 0, -1).normalized);
                yield return new TestCaseData(new Quaternion(-1, 0, -1, -1).normalized);
                yield return new TestCaseData(new Quaternion(0, -1, -1, -1).normalized);

                yield return new TestCaseData(new Quaternion(-1, -1, -1, -1).normalized);

                yield return new TestCaseData(Quaternion.Euler(200, 100, 10));
                yield return new TestCaseData(Quaternion.LookRotation(new Vector3(0.3f, 0.4f, 0.5f)));
                yield return new TestCaseData(Quaternion.Euler(45f, 56f, Mathf.PI));
                yield return new TestCaseData(Quaternion.AngleAxis(30, new Vector3(1, 2, 5)));
                yield return new TestCaseData(Quaternion.AngleAxis(5, new Vector3(-1, .01f, 0.44f)));
                yield return new TestCaseData(Quaternion.AngleAxis(358, new Vector3(0.5f, 2, 5)));
                yield return new TestCaseData(Quaternion.AngleAxis(-54, new Vector3(1, 2, 5)));
            }
        }

        [Test]
        [TestCaseSource(nameof(QuaternionTestCases))]
        public void RotationIsWithinPrecision(Quaternion rotationIn)
        {
            uint packed = Compression.CompressQuaternion(rotationIn);

            Quaternion rotationOut = Compression.DecompressQuaternion(packed);

            Assert.That(rotationOut.x, Is.Not.NaN, "x was NaN");
            Assert.That(rotationOut.y, Is.Not.NaN, "y was NaN");
            Assert.That(rotationOut.z, Is.Not.NaN, "z was NaN");
            Assert.That(rotationOut.w, Is.Not.NaN, "w was NaN");

            Vector3 inVec = rotationIn * Vector3.forward;
            Vector3 outVec = rotationOut * Vector3.forward;
            // allow for extra precision when rotating vector
            float precision = AllowedPrecision * 2;

            Assert.AreEqual(inVec.x, outVec.x, precision, $"x off by {Mathf.Abs(inVec.x - outVec.x)}");
            Assert.AreEqual(inVec.y, outVec.y, precision, $"y off by {Mathf.Abs(inVec.y - outVec.y)}");
            Assert.AreEqual(inVec.z, outVec.z, precision, $"z off by {Mathf.Abs(inVec.z - outVec.z)}");
        }


        [Test]
        [TestCaseSource(nameof(LargestIndexTestCases))]
        public void FindLargestIndexWork(Quaternion quaternion, int expected)
        {
            int largest = Compression.FindLargestIndex(quaternion);

            Assert.That(largest, Is.EqualTo(expected));
        }

        static IEnumerable LargestIndexTestCases
        {
            get
            {
                // args = Quaternion quaternion, int expected
                yield return new TestCaseData(new Quaternion(1, 0, 0, 0), 0);
                yield return new TestCaseData(new Quaternion(0, 1, 0, 0), 1);
                yield return new TestCaseData(new Quaternion(0, 0, 1, 0), 2);
                yield return new TestCaseData(new Quaternion(0, 0, 0, 1), 3);

                yield return new TestCaseData(new Quaternion(-1, 0, 0, 0), 0);
                yield return new TestCaseData(new Quaternion(0, -1, 0, 0), 1);
                yield return new TestCaseData(new Quaternion(0, 0, -1, 0), 2);
                yield return new TestCaseData(new Quaternion(0, 0, 0, -1), 3);

                yield return new TestCaseData(new Quaternion(1, 0, 0.5f, 0).normalized, 0);
                yield return new TestCaseData(new Quaternion(0, 1, 0.5f, 0).normalized, 1);
                yield return new TestCaseData(new Quaternion(0, 0.5f, 1, 0).normalized, 2);
                yield return new TestCaseData(new Quaternion(0, 0.5f, 0, 1).normalized, 3);

                yield return new TestCaseData(new Quaternion(-1, 0.9f, 0.5f, 0).normalized, 0);
                yield return new TestCaseData(new Quaternion(0.9f, -1, 0.5f, 0).normalized, 1);
                yield return new TestCaseData(new Quaternion(0, 0.5f, -1, 0.9f).normalized, 2);
                yield return new TestCaseData(new Quaternion(0, 0.5f, 0.9f, -1).normalized, 3);
            }
        }
    }
}
