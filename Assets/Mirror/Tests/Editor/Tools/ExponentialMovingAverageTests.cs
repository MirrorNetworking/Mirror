// tests from Mirror
using NUnit.Framework;

namespace Mirror.Tests.Tools
{
    [TestFixture]
    public class ExponentialMovingAverageTest
    {
        [Test]
        public void TestInitial()
        {
            ExponentialMovingAverage ema = new ExponentialMovingAverage(10);

            ema.Add(3);

            Assert.That(ema.Value, Is.EqualTo(3));
            Assert.That(ema.Variance, Is.EqualTo(0));
        }

        [Test]
        public void TestMovingAverage()
        {
            ExponentialMovingAverage ema = new ExponentialMovingAverage(10);

            ema.Add(5);
            ema.Add(6);

            Assert.That(ema.Value, Is.EqualTo(5.1818).Within(0.0001f));
            Assert.That(ema.Variance, Is.EqualTo(0.1487).Within(0.0001f));
        }

        [Test]
        public void TestVar()
        {
            ExponentialMovingAverage ema = new ExponentialMovingAverage(10);

            ema.Add(5);
            ema.Add(6);
            ema.Add(7);

            Assert.That(ema.Variance, Is.EqualTo(0.6134).Within(0.0001f));
        }

        [Test]
        public void TestStd()
        {
            ExponentialMovingAverage ema = new ExponentialMovingAverage(10);

            // large numbers to show that standard deviation is an absolute value
            ema.Add(5);
            ema.Add(600);
            ema.Add(70);

            Assert.That(ema.StandardDeviation, Is.EqualTo(208.2470).Within(0.0001f));
        }

        [Test]
        public void TestReset()
        {
            ExponentialMovingAverage ema = new ExponentialMovingAverage(10);

            // add some values and reset
            ema.Add(500);
            ema.Add(600);
            ema.Reset();

            // start again
            ema.Add(5);
            ema.Add(6);

            Assert.That(ema.Value, Is.EqualTo(5.1818).Within(0.0001f));
            Assert.That(ema.Variance, Is.EqualTo(0.1487).Within(0.0001f));
        }
    }
}
