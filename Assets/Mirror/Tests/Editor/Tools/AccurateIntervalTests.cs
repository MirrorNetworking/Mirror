using NUnit.Framework;

namespace Mirror.Tests.Tools
{
    public class AccurateIntervalTests
    {
        // regular test with 100ms interval and varying update calls
        [Test]
        public void Regular()
        {
            // 100 ms interval
            double lastTime = 0;
            double interval = 0.1;

            // shouldn't be elapsed if no time has passed yet
            Assert.That(AccurateInterval.Elapsed(0, interval, ref lastTime), Is.False);

            // check slightly before interval
            Assert.That(AccurateInterval.Elapsed(0.099, interval, ref lastTime), Is.False);

            // check with time 10ms after interval.
            // naive implementations would set 'last' to 'time' and drop the 10ms.
            // this is what AccurateInterval is trying to solve.
            Assert.That(AccurateInterval.Elapsed(0.110, interval, ref lastTime), Is.True); // +10 ms

            // should have been reset when elapsed
            Assert.That(AccurateInterval.Elapsed(0.110, interval, ref lastTime), Is.False);

            // see what happens if we go back in time
            Assert.That(AccurateInterval.Elapsed(0.099, interval, ref lastTime), Is.False);

            // next interval at 200.
            // this will only work if the +10ms from above weren't dropped.
            // otherwise last is at 110, so next would only be at 210.
            Assert.That(AccurateInterval.Elapsed(0.201, interval, ref lastTime), Is.True);
        }

        // test multiples of a second as well, not just fractions
        [Test]
        public void LargeInterval()
        {
            // 10s interval
            double lastTime = 0;
            double interval = 10;

            // shouldn't be elapsed if no time has passed yet
            Assert.That(AccurateInterval.Elapsed(0, interval, ref lastTime), Is.False);

            // check slightly before interval
            Assert.That(AccurateInterval.Elapsed(9.9, interval, ref lastTime), Is.False);

            // check with time 10ms after interval.
            // naive implementations would set 'last' to 'time' and drop the 10ms.
            // this is what AccurateInterval is trying to solve.
            Assert.That(AccurateInterval.Elapsed(10.01, interval, ref lastTime), Is.True); // +10 ms

            // should have been reset when elapsed
            Assert.That(AccurateInterval.Elapsed(10.01, interval, ref lastTime), Is.False);

            // see what happens if we go back in time
            Assert.That(AccurateInterval.Elapsed(9.9, interval, ref lastTime), Is.False);

            // next interval at 20.
            // this will only work if the +10ms from above weren't dropped.
            // otherwise last is at 110, so next would only be at 210.
            Assert.That(AccurateInterval.Elapsed(20.01, interval, ref lastTime), Is.True);
        }

        // heavy load test with 100ms interval and very slow update calls
        [Test]
        public void Slowdown()
        {
            // 100 ms interval
            double lastTime = 0;
            double interval = 0.1;

            // after 1s, it should be elapsed of course
            Assert.That(AccurateInterval.Elapsed(1.001, interval, ref lastTime), Is.True);

            // now the server has recovered.
            // back to normal update rate, 10ms later.
            //
            // we do not want to do any catch-up for the 10 missed intervals.
            // it should simply continue as usual.
            //
            // for virtual worlds / MMOs, we need to be able to recover from
            // heavy work loads, instead of making it worse with catch-up.
            Assert.That(AccurateInterval.Elapsed(1.011, interval, ref lastTime), Is.False);

            // finally, 100ms later it should trigger again
            Assert.That(AccurateInterval.Elapsed(1.101, interval, ref lastTime), Is.True);
        }

        // interval might change at runtime.
        // static function should support this too.
        [Test]
        public void ChangingInterval()
        {
            double lastTime = 0;

            // 100ms interval
            Assert.That(AccurateInterval.Elapsed(0,     0.100, ref lastTime), Is.False);
            Assert.That(AccurateInterval.Elapsed(0.101, 0.100, ref lastTime), Is.True);

            // continue with 50ms interval
            Assert.That(AccurateInterval.Elapsed(0.149, 0.050, ref lastTime), Is.False);
            Assert.That(AccurateInterval.Elapsed(0.151, 0.050, ref lastTime), Is.True);

            // back to a larger 200ms interval again
            Assert.That(AccurateInterval.Elapsed(0.349, 0.200, ref lastTime), Is.False);
            Assert.That(AccurateInterval.Elapsed(0.351, 0.200, ref lastTime), Is.True);
        }
    }
}
