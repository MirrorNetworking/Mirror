using NUnit.Framework;
using System.Threading;

namespace Mirror.Tests.Tools
{
    public class TimeSampleTests
    {
        [Test]
        public void Sample3()
        {
            // sample over 3 measurements
            TimeSample sample = new TimeSample(3);

            // initial without any values
            Assert.That(sample.average, Is.EqualTo(0));

            // measure 10ms. average should be 10ms.
            sample.Begin();
            Thread.Sleep(10);
            sample.End();
            Assert.That(sample.average, Is.EqualTo(0.010).Within(0.02));

            // measure 5ms. average should be 7.5ms
            sample.Begin();
            Thread.Sleep(5);
            sample.End();
            Assert.That(sample.average, Is.EqualTo(0.0075).Within(0.02));

            // measure 0ms. average should be 5ms.
            sample.Begin();
            Thread.Sleep(0);
            sample.End();
            Assert.That(sample.average, Is.EqualTo(0.005).Within(0.02));
        }
    }
}
