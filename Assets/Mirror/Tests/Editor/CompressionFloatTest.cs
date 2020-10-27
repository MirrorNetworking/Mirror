using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class CompressionFloatTest
    {
        [Test]
        [TestCaseSource(nameof(FloatTestCases))]
        public void CanScaleToUintAndBack(float value, float minFloat, float maxFloat, uint maxUint, float allowedPrecision)
        {
            uint packed = Compression.ScaleToUInt(value, minFloat, maxFloat, maxUint);
            float unpacked = Compression.ScaleFromUInt(packed, minFloat, maxFloat, maxUint);

            Assert.That(unpacked, Is.Not.NaN);
            Assert.That(unpacked, Is.EqualTo(value).Within(allowedPrecision), $"Value not in Allowed Precision\n   value    : {value}\n   unpacked: {unpacked}");
        }

        static IEnumerable FloatTestCases
        {
            get
            {
                // test values in various ranges with different min/max 
                foreach (object value in range(-2.2f, 2.5f, 0.1f, byte.MaxValue))
                {
                    yield return value;
                }
                foreach (object value in range(-200f, 200f, Mathf.PI, (1 << 10) - 1))
                {
                    yield return value;
                }
                foreach (object value in range(-0.7f, 0.7f, 0.03f, (1 << 19) - 1))
                {
                    yield return value;
                }
                foreach (object value in range(10f, 200f, 2f, (1 << 19) - 1))
                {
                    yield return value;
                }

                IEnumerable range(float min, float max, float step, uint uMax)
                {
                    float precision = (max - min) / uMax;
                    for (float f = min; f <= max; f += step)
                    {
                        yield return new TestCaseData(f, min, max, uMax, precision);
                    }
                }
            }
        }

        [Test]
        [TestCaseSource(nameof(InvalidRangeTestCases))]
        public void ShouldNotThrowWhenGivenInvalidValues(float value, float minFloat, float maxFloat, uint maxUint)
        {
            uint packed = Compression.ScaleToUInt(value, minFloat, maxFloat, maxUint);
            float unpacked = Compression.ScaleFromUInt(packed, minFloat, maxFloat, maxUint);

            // should not throw
        }

        static IEnumerable InvalidRangeTestCases
        {
            get
            {
                yield return new TestCaseData(0, 0, 0, 0u);
                yield return new TestCaseData(0, 0, 0, 1u);
                yield return new TestCaseData(0, 1, -1, byte.MaxValue);
                yield return new TestCaseData(0, 1.5f, 1.5f, byte.MaxValue);
            }
        }

        [Test]
        [TestCaseSource(nameof(OutOfRangeTestCases))]
        public uint ValuesOutoFrangeArePackedInRange(float value, float minFloat, float maxFloat, uint maxUint)
        {
            uint packed = Compression.ScaleToUInt(value, minFloat, maxFloat, maxUint);

            Assert.That(packed, Is.Not.NaN);

            return packed;
        }
        static IEnumerable OutOfRangeTestCases
        {
            get
            {
                float min = -1;
                float max = 1;
                uint uMax = 10;
                for (float f = -2; f <= min; f += 0.1f)
                {
                    yield return new TestCaseData(f, min, max, uMax).Returns(0);
                }
                for (float f = max; f <= 2; f += 0.1f)
                {
                    yield return new TestCaseData(f, min, max, uMax).Returns(10);
                }
            }
        }
    }
}
