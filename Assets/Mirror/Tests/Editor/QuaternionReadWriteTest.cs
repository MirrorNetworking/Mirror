using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class QuaternionReadWriteTest
    {
        // worse case where xyzw all equal error in largest is ~1.732 times greater than error in smallest 3
        // High/Low Precision fails when xyzw all equal,
        // *1.8f is enough to allow extra error in largest,
        internal const float HighestPrecision = 0.000000169f * 1.8f;
        internal const float MediumPrecision = 0.00138f;
        // *1.2f is enough to allow extra error in largest
        internal const float LowPrecision = 0.0110f * 1.2f; 

        [Test]
        [TestCaseSource(nameof(GetTestCases))]
        public void QuaternionCompressAtFullPrecision(Quaternion rotationIn)
        {
            QuaternionCompressWithinPrecision(rotationIn, RotationPrecision.Highest, HighestPrecision);
        }
        [Test]
        [TestCaseSource(nameof(GetTestCases))]
        public void QuaternionCompressAtHalfPrecision(Quaternion rotationIn)
        {
            QuaternionCompressWithinPrecision(rotationIn, RotationPrecision.Medium, MediumPrecision);
        }
        [Test]
        [TestCaseSource(nameof(GetTestCases))]
        public void QuaternionCompressAtLowPrecision(Quaternion rotationIn)
        {
            QuaternionCompressWithinPrecision(rotationIn, RotationPrecision.Low, LowPrecision);
        }

        static void QuaternionCompressWithinPrecision(Quaternion rotationIn, RotationPrecision precision, float allowedPrecision)
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteQuaternion(rotationIn, precision);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            Quaternion rotationOut = reader.ReadQuaternion(precision);

            Assert.That(rotationOut.x, Is.Not.NaN, "x was NaN");
            Assert.That(rotationOut.y, Is.Not.NaN, "y was NaN");
            Assert.That(rotationOut.z, Is.Not.NaN, "z was NaN");
            Assert.That(rotationOut.w, Is.Not.NaN, "w was NaN");

            Debug.LogFormat("In  {0}, {1}, {2}, {3}",
                rotationIn.x.ToString("N32"),
                rotationIn.y.ToString("N32"),
                rotationIn.z.ToString("N32"),
                rotationIn.w.ToString("N32")
                );
            Debug.LogFormat("Out {0}, {1}, {2}, {3}",
               rotationOut.x.ToString("N32"),
               rotationOut.y.ToString("N32"),
               rotationOut.z.ToString("N32"),
               rotationOut.w.ToString("N32")
               );

            AssertPrecision(rotationIn, rotationOut, allowedPrecision);
        }

        internal static void AssertPrecision(Quaternion inRot, Quaternion outRot, float precision)
        {
            int largest = QuaternionReadWrite.FindLargestIndex(inRot);
            float sign = Mathf.Sign(inRot[largest]);
            // flip sign of A if largest is is negative
            // Q == (-Q)

            Assert.AreEqual(sign * inRot.x, outRot.x, precision, $"x off by {Mathf.Abs(sign * inRot.x - outRot.x)}");
            Assert.AreEqual(sign * inRot.y, outRot.y, precision, $"y off by {Mathf.Abs(sign * inRot.y - outRot.y)}");
            Assert.AreEqual(sign * inRot.z, outRot.z, precision, $"z off by {Mathf.Abs(sign * inRot.z - outRot.z)}");
            Assert.AreEqual(sign * inRot.w, outRot.w, precision, $"w off by {Mathf.Abs(sign * inRot.w - outRot.w)}");
        }

        static object[] GetTestCases()
        {
            List<Quaternion> list = new List<Quaternion>
            {
                Quaternion.identity,
                new Quaternion(1, 0, 0, 0),
                new Quaternion(0, 1, 0, 0),
                new Quaternion(0, 0, 1, 0),

                new Quaternion(1, 1, 0, 0).normalized,
                new Quaternion(0, 1, 1, 0).normalized,
                new Quaternion(0, 1, 1, 0).normalized,
                new Quaternion(0, 0, 1, 1).normalized,

                new Quaternion(1, 1, 1, 0).normalized,
                new Quaternion(1, 1, 0, 1).normalized,
                new Quaternion(1, 0, 1, 1).normalized,
                new Quaternion(0, 1, 1, 1).normalized,

                new Quaternion(1, 1, 1, 1).normalized,

                new Quaternion(-1, 0, 0, 0),
                new Quaternion(0, -1, 0, 0),
                new Quaternion(0, 0, -1, 0),
                new Quaternion(0, 0, 0, -1),

                new Quaternion(-1, -1, 0, 0).normalized,
                new Quaternion(0, -1, -1, 0).normalized,
                new Quaternion(0, -1, -1, 0).normalized,
                new Quaternion(0, 0, -1, -1).normalized,

                new Quaternion(-1, -1, -1, 0).normalized,
                new Quaternion(-1, -1, 0, -1).normalized,
                new Quaternion(-1, 0, -1, -1).normalized,
                new Quaternion(0, -1, -1, -1).normalized,

                new Quaternion(-1, -1, -1, -1).normalized,

                Quaternion.Euler(200, 100, 10),
                Quaternion.LookRotation(new Vector3(0.3f, 0.4f, 0.5f)),
                Quaternion.Euler(45f, 56f, Mathf.PI),
                Quaternion.AngleAxis(30, new Vector3(1, 2, 5)),
                Quaternion.AngleAxis(5, new Vector3(-1, .01f, 0.44f)),
                Quaternion.AngleAxis(358, new Vector3(0.5f, 2, 5)),
                Quaternion.AngleAxis(-54, new Vector3(1, 2, 5)),
            };

            int count = list.Count;
            object[] cases = new object[count];
            for (int i = 0; i < count; i++)
            {
                cases[i] = new object[] { list[i] };
            }

            return cases;
        }


        [Test]
        public void WriteQuaternionCorrectLengthForFull()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteQuaternion(Quaternion.identity, RotationPrecision.Highest);

            Assert.That(writer.ToArray().Length, Is.EqualTo(9));
        }
        [Test]
        public void WriteQuaternionCorrectLengthForHalf()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteQuaternion(Quaternion.identity, RotationPrecision.Medium);

            Assert.That(writer.ToArray().Length, Is.EqualTo(4));
        }
        [Test]
        public void WriteQuaternionCorrectLengthForNoRotation()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteQuaternion(Quaternion.identity, RotationPrecision.NoRotation);

            Assert.That(writer.ToArray().Length, Is.Zero);
        }

        [Test]
        [TestCaseSource(nameof(GetLargestIndexTestCases))]
        public void FindLargestIndexWork(Quaternion quaternion, int expected)
        {
            int largest = QuaternionReadWrite.FindLargestIndex(quaternion);

            Assert.That(largest, Is.EqualTo(expected));
        }
        static object[] GetLargestIndexTestCases()
        {
            return new object[]
            {
                // args = Quaternion quaternion, int expected
                new object[] { new Quaternion(1, 0, 0, 0), 0 },
                new object[] { new Quaternion(0, 1, 0, 0), 1 },
                new object[] { new Quaternion(0, 0, 1, 0), 2 },
                new object[] { new Quaternion(0, 0, 0, 1), 3 },

                new object[] { new Quaternion(-1, 0, 0, 0), 0 },
                new object[] { new Quaternion(0, -1, 0, 0), 1 },
                new object[] { new Quaternion(0, 0, -1, 0), 2 },
                new object[] { new Quaternion(0, 0, 0, -1), 3 },

                new object[] { new Quaternion(1, 0, 0.5f, 0).normalized, 0 },
                new object[] { new Quaternion(0, 1, 0.5f, 0).normalized, 1 },
                new object[] { new Quaternion(0, 0.5f, 1, 0).normalized, 2 },
                new object[] { new Quaternion(0, 0.5f, 0, 1).normalized, 3 },

                new object[] { new Quaternion(-1, 0.9f, 0.5f, 0).normalized, 0 },
                new object[] { new Quaternion(0.9f, -1, 0.5f, 0).normalized, 1 },
                new object[] { new Quaternion(0, 0.5f, -1, 0.9f).normalized, 2 },
                new object[] { new Quaternion(0, 0.5f, 0.9f, -1).normalized, 3 },
            };
        }

    }
}
