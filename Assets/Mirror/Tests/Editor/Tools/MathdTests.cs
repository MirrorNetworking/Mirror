using NUnit.Framework;

namespace Mirror.Tests.Tools
{
    public class MathdTests
    {
        [Test]
        public void Clamp01()
        {
            Assert.That(Mathd.Clamp01(-0.01), Is.EqualTo(0));
            Assert.That(Mathd.Clamp01(0.5), Is.EqualTo(0.5));
            Assert.That(Mathd.Clamp01(1.01), Is.EqualTo(1));
        }

        [Test]
        public void LerpUnclamped()
        {
            Assert.That(Mathd.LerpUnclamped(-1, 1, 0.5), Is.EqualTo(0));
            Assert.That(Mathd.LerpUnclamped(-1, 1, 1.5), Is.EqualTo(2));
        }

        [Test]
        public void InverseLerp()
        {
            Assert.That(Mathd.InverseLerp(-1, 1, 0), Is.EqualTo(0.5));
            Assert.That(Mathd.InverseLerp(0, 10, 0), Is.EqualTo(0));
            Assert.That(Mathd.InverseLerp(0, 10, 5), Is.EqualTo(0.5));
            Assert.That(Mathd.InverseLerp(0, 10, 10), Is.EqualTo(1));
            Assert.That(Mathd.InverseLerp(0, 10, 15), Is.EqualTo(1)); // 1.5 but clamped
        }
    }
}
