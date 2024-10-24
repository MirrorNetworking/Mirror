// we are using the dotnet/runtime implementation which is already tested.
// basic test to ensure it generally works as expected.

using System;
using NUnit.Framework;

namespace Mirror.Tests.Tools
{
    public class HalfTests
    {
        [Test]
        public void BasicTest()
        {
            // start from a float
            Half half = (Half)42.0f;
            Half other = (Half)0.5f;

            // add
            Half value = half + other;
            Assert.That((float)value, Is.EqualTo(42.5f));

            // sub
            value = half - other;
            Assert.That((float)value, Is.EqualTo(41.5f));

            // compare
            Assert.That(half < other, Is.False);
            Assert.That(half > other, Is.True);
            Assert.That(half == other, Is.False);
            Assert.That(half != other, Is.True);
            Assert.That(half, Is.EqualTo((Half)42.0f));
            Assert.That(half == (Half)42.0f, Is.True);
        }
    }
}
