using NUnit.Framework;
using System.Collections.Generic;

namespace Mirror.Tests
{
    // a simple snapshot with timestamp & interpolation
    struct SimpleSnapshot : Snapshot
    {
        public double remoteTime { get; set; }
        public double localTime  { get; set; }
        public double value;

        public SimpleSnapshot(double remoteTime, double localTime, double value)
        {
            this.remoteTime = remoteTime;
            this.localTime = localTime;
            this.value = value;
        }

        public static SimpleSnapshot Interpolate(SimpleSnapshot from, SimpleSnapshot to, double t) =>
            new SimpleSnapshot(
                // interpolated snapshot is applied directly. don't need timestamps.
                0, 0,
                // lerp unclamped in case we ever need to extrapolate.
                // atm SnapshotInterpolation never does.
                Mathd.LerpUnclamped(from.value, to.value, t));
    }

    public class SnapshotInterpolationTests
    {
        // buffer for convenience so we don't have to create it manually each time
        SortedList<double, SimpleSnapshot> buffer;

        // some defaults
        const double catchupSpeed   = 0.02;
        const double slowdownSpeed  = 0.04;
        const double negativeThresh = -0.10; // in seconds
        const double positiveThresh =  0.10; // in seconds
        const int bufferLimit = 32;

        [SetUp]
        public void SetUp()
        {
            buffer = new SortedList<double, SimpleSnapshot>();
        }

        [Test]
        public void RemoveRange()
        {
            buffer.Add(1, default);
            buffer.Add(2, default);
            buffer.Add(3, default);

            // remove negative
            buffer.RemoveRange(-1);
            Assert.That(buffer.Count, Is.EqualTo(3));

            // remove none
            buffer.RemoveRange(0);
            Assert.That(buffer.Count, Is.EqualTo(3));

            // remove multiple
            buffer.RemoveRange(2);
            Assert.That(buffer.Count, Is.EqualTo(1));
            Assert.That(buffer.ContainsKey(3), Is.True);

            // remove more than it has
            buffer.RemoveRange(2);
            Assert.That(buffer.Count, Is.EqualTo(0));
        }

        [Test]
        public void Timescale()
        {
            // no drift: linear time
            Assert.That(SnapshotInterpolation.Timescale(0, catchupSpeed, slowdownSpeed, negativeThresh, positiveThresh), Is.EqualTo(1.0));

            // near negative thresh but not under it: linear time
            Assert.That(SnapshotInterpolation.Timescale(-0.09, catchupSpeed, slowdownSpeed, negativeThresh, positiveThresh), Is.EqualTo(1.0));

            // near positive thresh but not above it: linear time
            Assert.That(SnapshotInterpolation.Timescale(0.09, catchupSpeed, slowdownSpeed, negativeThresh, positiveThresh), Is.EqualTo(1.0));

            // below negative thresh: catchup
            Assert.That(SnapshotInterpolation.Timescale(-0.11, catchupSpeed, slowdownSpeed, negativeThresh, positiveThresh), Is.EqualTo(0.96));

            // above positive thresh: slowdown
            Assert.That(SnapshotInterpolation.Timescale(0.11, catchupSpeed, slowdownSpeed, negativeThresh, positiveThresh), Is.EqualTo(1.02));
        }

        [Test]
        public void TimelineClamp()
        {
            // latest snapshot at 1, with buffer of 0.5.
            // => target time is latest-buffer = 0.5
            // => bounds are target +- buffer, so [0, 1]

            // within bounds
            Assert.That(SnapshotInterpolation.TimelineClamp(0, 0.5, 1), Is.EqualTo(0));
            Assert.That(SnapshotInterpolation.TimelineClamp(0.5, 0.5, 1), Is.EqualTo(0.5));
            Assert.That(SnapshotInterpolation.TimelineClamp(1, 0.5, 1), Is.EqualTo(1));

            // behind: clamps to lower bound
            Assert.That(SnapshotInterpolation.TimelineClamp(-1, 0.5, 1), Is.EqualTo(0));

            // ahead: clamps to upper bound
            Assert.That(SnapshotInterpolation.TimelineClamp(2, 0.5, 1), Is.EqualTo(1));
        }

        [Test]
        public void DynamicAdjustment()
        {
            // 100ms send interval, 0ms std jitter, 0.5 (50%) tolerance
            // -> sendInterval+jitter = 100ms
            // -> that's 1x sendInterval
            // -> add 0.5x tolerance
            // => 1.5x buffer multiplier
            Assert.That(SnapshotInterpolation.DynamicAdjustment(0.100, 0.000, 0.5), Is.EqualTo(1.5).Within(0.0001));

            // 100ms send interval, 10ms std jitter, 0.5 (50%) tolerance
            // -> sendInterval+jitter = 110ms
            // -> that's 1.1x sendInterval
            // -> add 0.5x tolerance
            // => 1.6x buffer multiplier
            Assert.That(SnapshotInterpolation.DynamicAdjustment(0.100, 0.010, 0.5), Is.EqualTo(1.6).Within(0.0001));
        }

        [Test]
        public void InsertIfNotExists()
        {
            // example snaps
            SimpleSnapshot a = new SimpleSnapshot(2, 0, 42);
            SimpleSnapshot b = new SimpleSnapshot(3, 0, 43);

            // add a
            Assert.True(SnapshotInterpolation.InsertIfNotExists(buffer, bufferLimit, a));
            Assert.That(buffer.Count, Is.EqualTo(1));
            Assert.That(buffer.Values[0], Is.EqualTo(a));

            // add a again - shouldn't do anything
            Assert.False(SnapshotInterpolation.InsertIfNotExists(buffer, bufferLimit, a));
            Assert.That(buffer.Count, Is.EqualTo(1));
            Assert.That(buffer.Values[0], Is.EqualTo(a));

            // add b
            Assert.True(SnapshotInterpolation.InsertIfNotExists(buffer, bufferLimit, b));
            Assert.That(buffer.Count, Is.EqualTo(2));
            Assert.That(buffer.Values[0], Is.EqualTo(a));
            Assert.That(buffer.Values[1], Is.EqualTo(b));

            // add b again - shouldn't do anything
            Assert.False(SnapshotInterpolation.InsertIfNotExists(buffer, bufferLimit, b));
            Assert.That(buffer.Count, Is.EqualTo(2));
            Assert.That(buffer.Values[0], Is.EqualTo(a));
            Assert.That(buffer.Values[1], Is.EqualTo(b));
        }

        [Test]
        public void InsertIfNotExists_RespectsBufferLimit()
        {
            // guarantee that we can never insert more than buffer limit
            for (int i = 0; i < bufferLimit * 2; ++i)
            {
                SimpleSnapshot snap = new SimpleSnapshot(i, i, i);
                SnapshotInterpolation.InsertIfNotExists(buffer, bufferLimit, snap);
            }
            Assert.That(buffer.Count, Is.EqualTo(bufferLimit));
        }

        // UDP packets may arrive twice with the same snapshot.
        // inserting twice needs to be handled without throwing exceptions.
        [Test]
        public void InsertTwice()
        {
            // defaults
            ExponentialMovingAverage driftEma            = default;
            ExponentialMovingAverage deliveryIntervalEma = default;
            SimpleSnapshot           snap                = default;

            double localTimeline  = 0;
            double localTimescale = 0;

            // insert twice
            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, snap, ref localTimeline, ref localTimescale, 0, 0, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, snap, ref localTimeline, ref localTimescale, 0, 0, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);

            // should only be inserted once
            Assert.That(buffer.Count, Is.EqualTo(1));
        }

        [Test]
        public void Insert_Sorts()
        {
            // defaults
            ExponentialMovingAverage driftEma            = default;
            ExponentialMovingAverage deliveryIntervalEma = default;

            double localTimeline  = 0;
            double localTimescale = 0;

            // example snaps
            SimpleSnapshot a = new SimpleSnapshot(2, 0, 42);
            SimpleSnapshot b = new SimpleSnapshot(3, 0, 43);

            // insert in reverse order
            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, b, ref localTimeline, ref localTimescale, 0, 0, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, a, ref localTimeline, ref localTimescale, 0, 0, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);

            // should be in sorted order
            Assert.That(buffer.Count, Is.EqualTo(2));
            Assert.That(buffer.Values[0], Is.EqualTo(a));
            Assert.That(buffer.Values[1], Is.EqualTo(b));
        }

        [Test]
        public void Insert_InitializesLocalTimeline()
        {
            // defaults
            ExponentialMovingAverage driftEma            = default;
            ExponentialMovingAverage deliveryIntervalEma = default;

            double localTimeline  = 0;
            double localTimescale = 0;
            double bufferTime = 3; // don't move timeline until all 3 inserted

            // example snaps
            SimpleSnapshot a = new SimpleSnapshot(2, 0, 42);
            SimpleSnapshot b = new SimpleSnapshot(3, 0, 43);

            // first insertion should initialize the local timeline to remote time
            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, a, ref localTimeline, ref localTimescale, 0, bufferTime, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(2 - bufferTime)); // initial snapshot - buffer time

            // second insertion should not modify the timeline again
            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, b, ref localTimeline, ref localTimescale, 0, bufferTime, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(2 - bufferTime)); // initial snapshot - buffer time
        }

        [Test]
        public void Insert_ComputesAverageDrift()
        {
            // defaults: drift ema with 3 values
            ExponentialMovingAverage driftEma            = new ExponentialMovingAverage(3);
            ExponentialMovingAverage deliveryIntervalEma = default;

            double localTimeline  = 0;
            double localTimescale = 0;
            double bufferTime = 3; // don't move timeline until all 3 inserted

            // example snaps
            SimpleSnapshot a = new SimpleSnapshot(2, 0, 42);
            SimpleSnapshot b = new SimpleSnapshot(3, 0, 43);
            SimpleSnapshot c = new SimpleSnapshot(5, 0, 43);

            // insert in order
            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, a, ref localTimeline, ref localTimescale, 0, bufferTime, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(2 - bufferTime)); // initial snapshot - buffer time

            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, b, ref localTimeline, ref localTimescale, 0, bufferTime, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(2 - bufferTime)); // initial snapshot - buffer time

            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, c, ref localTimeline, ref localTimescale, 0, bufferTime, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(2 - bufferTime)); // initial snapshot - buffer time

            // first insertion initializes localTime to '2'.
            // so the timediffs to '2' are: 0, 1, 3.
            // which gives an ema of 1.75
            Assert.That(driftEma.Value, Is.EqualTo(bufferTime + 1.75));
        }

        [Test]
        public void Insert_ComputesAverageDrift_Scrambled()
        {
            // defaults: drift ema with 3 values
            ExponentialMovingAverage driftEma            = new ExponentialMovingAverage(3);
            ExponentialMovingAverage deliveryIntervalEma = default;

            double localTimeline  = 0;
            double localTimescale = 0;
            double bufferTime = 3; // don't move timeline until all 3 inserted

            // example snaps
            SimpleSnapshot a = new SimpleSnapshot(2, 0, 42);
            SimpleSnapshot b = new SimpleSnapshot(3, 0, 43);
            SimpleSnapshot c = new SimpleSnapshot(5, 0, 43);

            // insert scrambled (not in order)
            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, a, ref localTimeline, ref localTimescale, 0, bufferTime, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(2 - bufferTime)); // initial snapshot - buffer time

            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, c, ref localTimeline, ref localTimescale, 0, bufferTime, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(2 - bufferTime)); // initial snapshot - buffer time

            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, b, ref localTimeline, ref localTimescale, 0, bufferTime, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(2 - bufferTime)); // initial snapshot - buffer time

            // first insertion initializes localTime to '2'.
            // so the timediffs to '2' are: 0, 3, 1.
            // which gives an ema of 1.25
            //
            // originally timeDiff was always computed from buffer[count-1],
            // which would be 0, 3, 3, which would give a (wrong) ema of 2.25.
            Assert.That(driftEma.Value, Is.EqualTo(bufferTime + 1.25));
        }

        [Test]
        public void Insert_ComputesAverageDeliveryInterval()
        {
            // defaults: delivery ema with 2 values
            // because delivery time ema is always between 2 snaps.
            // so for 3 values, it's only computed twice.
            ExponentialMovingAverage driftEma            = new ExponentialMovingAverage(2);
            ExponentialMovingAverage deliveryIntervalEma = new ExponentialMovingAverage(2);

            double localTimeline  = 0;
            double localTimescale = 0;
            double bufferTime = 3; // don't move timeline until all 3 inserted

            // example snaps with local arrival times
            SimpleSnapshot a = new SimpleSnapshot(2, 3, 42);
            SimpleSnapshot b = new SimpleSnapshot(3, 4, 43);
            SimpleSnapshot c = new SimpleSnapshot(5, 6, 43);

            // insert in order
            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, a, ref localTimeline, ref localTimescale, 0, bufferTime, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(2 - bufferTime)); // initial snapshot - buffer time

            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, b, ref localTimeline, ref localTimescale, 0, bufferTime, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(2 - bufferTime)); // initial snapshot - buffer time

            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, c, ref localTimeline, ref localTimescale, 0, bufferTime, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(2 - bufferTime)); // initial snapshot - buffer time

            // first insertion doesn't compute delivery interval because we need 2 snaps.
            // second insertion computes 4-3 = 1
            // third insertion computes  6-4 = 2
            // which gives an ema of:         2.2222
            // with a standard deviation of:  1.1331
            Assert.That(driftEma.Value, Is.EqualTo(bufferTime + 2.2222).Within(0.0001));
            Assert.That(driftEma.StandardDeviation, Is.EqualTo(1.1331).Within(0.0001));
        }

        [Test, Ignore("Delivery Time EMA doesn't handle scrambled packages differently yet")]
        public void Insert_ComputesAverageDeliveryInterval_Scrambled()
        {
            // defaults: delivery ema with 2 values
            // because delivery time ema is always between 2 snaps.
            // so for 3 values, it's only computed twice.
            ExponentialMovingAverage driftEma            = new ExponentialMovingAverage(2);
            ExponentialMovingAverage deliveryIntervalEma = new ExponentialMovingAverage(2);

            double localTimeline  = 0;
            double localTimescale = 0;

            // example snaps with local arrival times
            SimpleSnapshot a = new SimpleSnapshot(2, 3, 42);
            SimpleSnapshot b = new SimpleSnapshot(3, 4, 43);
            SimpleSnapshot c = new SimpleSnapshot(5, 6, 43);

            // insert in order
            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, a, ref localTimeline, ref localTimescale, 0, 0, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(2)); // detect wrong timeline immediately

            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, c, ref localTimeline, ref localTimescale, 0, 0, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(2)); // detect wrong timeline immediately

            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, b, ref localTimeline, ref localTimescale, 0, 0, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(2)); // detect wrong timeline immediately


            // first insertion doesn't compute delivery interval because we need 2 snaps.
            // second insertion computes 4-3 = 1
            // third insertion computes  6-4 = 2
            // which gives an ema of:         2.2222
            // with a standard deviation of:  1.1331
            Assert.That(driftEma.Value, Is.EqualTo(2.2222).Within(0.0001));
            Assert.That(driftEma.StandardDeviation, Is.EqualTo(1.1331).Within(0.0001));
        }

        [Test]
        public void Sample()
        {
            // defaults
            ExponentialMovingAverage driftEma            = default;
            ExponentialMovingAverage deliveryIntervalEma = default;

            double localTimeline  = 0;
            double localTimescale = 0;
            double bufferTime = 30; // don't move timeline until all 3 inserted

            // example snaps
            SimpleSnapshot a = new SimpleSnapshot(10, 0, 42);
            SimpleSnapshot b = new SimpleSnapshot(20, 0, 43);
            SimpleSnapshot c = new SimpleSnapshot(30, 0, 44);

            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, a, ref localTimeline, ref localTimescale, 0, bufferTime, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(10-bufferTime)); // initial snapshot - buffer time

            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, b, ref localTimeline, ref localTimescale, 0, bufferTime, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(10-bufferTime)); // initial snapshot - buffer time

            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, c, ref localTimeline, ref localTimescale, 0, bufferTime, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(10-bufferTime)); // initial snapshot - buffer time

            // sample at a time before the first snapshot
            SnapshotInterpolation.Sample(buffer, 9, out int from, out int to, out double t);
            Assert.That(from, Is.EqualTo(0));
            Assert.That(to, Is.EqualTo(0));
            Assert.That(t, Is.EqualTo(0));

            // sample inbetween 2nd and 3rd snapshots
            SnapshotInterpolation.Sample(buffer, 22.5, out from, out to, out t);
            Assert.That(from, Is.EqualTo(1)); // second
            Assert.That(to, Is.EqualTo(2));   // third
            Assert.That(t, Is.EqualTo(0.25)); // exactly in the middle

            // sample at a time after the third snapshot
            SnapshotInterpolation.Sample(buffer, 31, out from, out to, out t);
            Assert.That(from, Is.EqualTo(2)); // third
            Assert.That(to, Is.EqualTo(2));   // third
            Assert.That(t, Is.EqualTo(0));
        }

        [Test]
        public void Step()
        {
            // defaults
            ExponentialMovingAverage driftEma            = default;
            ExponentialMovingAverage deliveryIntervalEma = default;

            double localTimeline  = 0;
            double localTimescale = 0;
            double bufferTime = 30; // don't move timeline until all 3 inserted

            // example snaps
            SimpleSnapshot a = new SimpleSnapshot(10, 0, 42);
            SimpleSnapshot b = new SimpleSnapshot(20, 0, 43);
            SimpleSnapshot c = new SimpleSnapshot(30, 0, 44);

            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, a, ref localTimeline, ref localTimescale, 0, bufferTime, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(10-bufferTime)); // initial snapshot - buffer time

            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, b, ref localTimeline, ref localTimescale, 0, bufferTime, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(10-bufferTime)); // initial snapshot - buffer time

            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, c, ref localTimeline, ref localTimescale, 0, bufferTime, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(10-bufferTime)); // initial snapshot - buffer time

            // step half way to the next snapshot
            SnapshotInterpolation.Step(buffer, bufferTime + 5, ref localTimeline, localTimescale, out SimpleSnapshot fromSnapshot, out SimpleSnapshot toSnapshot, out double t);
            SimpleSnapshot computed = SimpleSnapshot.Interpolate(fromSnapshot, toSnapshot, t);
            Assert.That(computed.value, Is.EqualTo(42.5));
        }

        [Test]
        public void Step_RemovesOld()
        {
            // defaults
            ExponentialMovingAverage driftEma            = default;
            ExponentialMovingAverage deliveryIntervalEma = default;

            double localTimeline  = 0;
            double localTimescale = 0;

            // example snaps
            SimpleSnapshot a = new SimpleSnapshot(10, 0, 42);
            SimpleSnapshot b = new SimpleSnapshot(20, 0, 43);
            SimpleSnapshot c = new SimpleSnapshot(30, 0, 44);
            double bufferTime = 30; // don't move timeline until all 3 inserted

            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, a, ref localTimeline, ref localTimescale, 0, bufferTime, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(10-bufferTime)); // initial snapshot - buffer time

            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, b, ref localTimeline, ref localTimescale, 0, bufferTime, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(10-bufferTime)); // initial snapshot - buffer time

            SnapshotInterpolation.InsertAndAdjust(buffer, bufferLimit, c, ref localTimeline, ref localTimescale, 0, bufferTime, 0.01, 0.01, ref driftEma, 0, 0, ref deliveryIntervalEma);
            Assert.That(localTimeline, Is.EqualTo(10-bufferTime)); // initial snapshot - buffer time

            // step 1.5 snapshots worth, so way past the first one
            SnapshotInterpolation.Step(buffer, bufferTime + 15, ref localTimeline, localTimescale, out SimpleSnapshot fromSnapshot, out SimpleSnapshot toSnapshot, out double t);
            SimpleSnapshot computed = SimpleSnapshot.Interpolate(fromSnapshot, toSnapshot, t);
            Assert.That(computed.value, Is.EqualTo(43.5));

            // first snapshot should've been removed since we stepped past it
            Assert.That(buffer.Count, Is.EqualTo(2));
            Assert.That(buffer.Values[0], Is.EqualTo(b));
            Assert.That(buffer.Values[1], Is.EqualTo(c));
        }
    }
}
