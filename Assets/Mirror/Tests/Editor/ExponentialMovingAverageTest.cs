using NUnit.Framework;
namespace Mirror.Tests
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
            Assert.That(ema.Var, Is.EqualTo(0));
        }

        [Test]
        public void TestMovingAverage()
        {
            ExponentialMovingAverage ema = new ExponentialMovingAverage(10);

            ema.Add(5);
            ema.Add(6);

            Assert.That(ema.Value, Is.EqualTo(5.1818).Within(0.0001f));
            Assert.That(ema.Var, Is.EqualTo(0.1487).Within(0.0001f));
        }

        [Test]
        public void TestVar()
        {
            ExponentialMovingAverage ema = new ExponentialMovingAverage(10);

            ema.Add(5);
            ema.Add(6);
            ema.Add(7);

            Assert.That(ema.Var, Is.EqualTo(0.6134).Within(0.0001f));
        }
    }
}
