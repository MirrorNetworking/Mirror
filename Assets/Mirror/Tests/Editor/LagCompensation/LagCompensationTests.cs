using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Mirror.Tests.LagCompensationTests
{
    // a simple snapshot with timestamp & interpolation
    struct SimpleCapture : Capture
    {
        public double timestamp { get; set; }
        public int value;

        public SimpleCapture(double timestamp, int value)
        {
            this.timestamp = timestamp;
            this.value = value;
        }

        public void DrawGizmo() {}
    }

    public class LagCompensationTests
    {
        // buffer for convenience so we don't have to create it manually each time
        Queue<KeyValuePair<double, SimpleCapture>> history;

        // some defaults
        const int HistoryLimit = 4;
        const double Interval = 1;

        [SetUp]
        public void SetUp()
        {
            history = new Queue<KeyValuePair<double, SimpleCapture>>();
        }

        [Test]
        public void Insert()
        {
            // insert a few
            LagCompensation.Insert(history, HistoryLimit, 1, new SimpleCapture(1, 10));
            LagCompensation.Insert(history, HistoryLimit, 2, new SimpleCapture(2, 20));
            LagCompensation.Insert(history, HistoryLimit, 3, new SimpleCapture(3, 30));

            Assert.That(history.Count, Is.EqualTo(3));
            Assert.That(history.ElementAt(0).Key, Is.EqualTo(1));
            Assert.That(history.ElementAt(1).Key, Is.EqualTo(2));
            Assert.That(history.ElementAt(2).Key, Is.EqualTo(3));
            Assert.That(history.ElementAt(0).Value.value, Is.EqualTo(10));
            Assert.That(history.ElementAt(1).Value.value, Is.EqualTo(20));
            Assert.That(history.ElementAt(2).Value.value, Is.EqualTo(30));
        }

        [Test]
        public void InsertAboveLimit()
        {
            // inserting more than limit, should evict the oldest one
            LagCompensation.Insert(history, HistoryLimit, 1, new SimpleCapture(1, 10));
            LagCompensation.Insert(history, HistoryLimit, 2, new SimpleCapture(2, 20));
            LagCompensation.Insert(history, HistoryLimit, 3, new SimpleCapture(3, 30));
            LagCompensation.Insert(history, HistoryLimit, 4, new SimpleCapture(4, 40));
            LagCompensation.Insert(history, HistoryLimit, 5, new SimpleCapture(5, 50));

            Assert.That(history.Count, Is.EqualTo(4));
            Assert.That(history.ElementAt(0).Key, Is.EqualTo(2));
            Assert.That(history.ElementAt(1).Key, Is.EqualTo(3));
            Assert.That(history.ElementAt(2).Key, Is.EqualTo(4));
            Assert.That(history.ElementAt(3).Key, Is.EqualTo(5));
            Assert.That(history.ElementAt(0).Value.value, Is.EqualTo(20));
            Assert.That(history.ElementAt(1).Value.value, Is.EqualTo(30));
            Assert.That(history.ElementAt(2).Value.value, Is.EqualTo(40));
            Assert.That(history.ElementAt(3).Value.value, Is.EqualTo(50));
        }

        [Test]
        public void Sample_Empty()
        {
            Assert.That(LagCompensation.Sample(history, 0, Interval, out _, out _, out _), Is.False);
        }

        [Test]
        public void Sample_Single_Interpolate()
        {
            // always need at least two values
            LagCompensation.Insert(history, HistoryLimit, 1, new SimpleCapture(1, 10));
            Assert.That(LagCompensation.Sample(history, 0.5, Interval, out _, out _, out _), Is.False);
        }

        [Test]
        public void Sample_Single_Extrapolate()
        {
            // insert a few
            LagCompensation.Insert(history, HistoryLimit, 1, new SimpleCapture(1, 10));
            Assert.That(LagCompensation.Sample(history, 1.5, Interval, out _, out _, out _), Is.False);
        }

        [Test]
        public void Sample_Double_Interpolate()
        {
            // always need at least two values
            LagCompensation.Insert(history, HistoryLimit, 1, new SimpleCapture(1, 10));
            LagCompensation.Insert(history, HistoryLimit, 2, new SimpleCapture(2, 20));

            // sample older than first
            Assert.That(LagCompensation.Sample(history, 0, Interval, out SimpleCapture before, out SimpleCapture after, out double t), Is.False);

            // sample exactly first
            Assert.That(LagCompensation.Sample(history, 1, Interval, out before, out after, out t), Is.True);
            Assert.That(before.value, Is.EqualTo(10));
            Assert.That(after.value, Is.EqualTo(10));
            Assert.That(t, Is.EqualTo(0.0));

            // sample between first and second
            Assert.That(LagCompensation.Sample(history, 1.5, Interval, out before, out after, out t), Is.True);
            Assert.That(before.value, Is.EqualTo(10));
            Assert.That(after.value, Is.EqualTo(20));
            Assert.That(t, Is.EqualTo(0.5));
        }

        [Test]
        public void Sample_Double_Extrapolate()
        {
            // insert a few
            LagCompensation.Insert(history, HistoryLimit, 1, new SimpleCapture(1, 10));
            LagCompensation.Insert(history, HistoryLimit, 2, new SimpleCapture(2, 20));

            // sample newer than newest: should extrapolate even if we only have one entry
            Assert.That(LagCompensation.Sample(history, 2.5, Interval, out SimpleCapture before, out SimpleCapture after, out double t), Is.True);
            Assert.That(before.value, Is.EqualTo(10));
            Assert.That(after.value, Is.EqualTo(20));
            Assert.That(t, Is.EqualTo(1.5));
        }

        [Test]
        public void Sample_Triple_Interpolate()
        {
            // insert a few
            LagCompensation.Insert(history, HistoryLimit, 1, new SimpleCapture(1, 10));
            LagCompensation.Insert(history, HistoryLimit, 2, new SimpleCapture(2, 20));
            LagCompensation.Insert(history, HistoryLimit, 3, new SimpleCapture(3, 30));

            // sample older than first
            Assert.That(LagCompensation.Sample(history, 0, Interval, out SimpleCapture before, out SimpleCapture after, out double t), Is.False);

            // sample exactly first
            Assert.That(LagCompensation.Sample(history, 1, Interval, out before, out after, out t), Is.True);
            Assert.That(before.value, Is.EqualTo(10));
            Assert.That(after.value, Is.EqualTo(10));
            Assert.That(t, Is.EqualTo(0.0));

            // sample between first and second
            Assert.That(LagCompensation.Sample(history, 1.5, Interval, out before, out after, out t), Is.True);
            Assert.That(before.value, Is.EqualTo(10));
            Assert.That(after.value, Is.EqualTo(20));
            Assert.That(t, Is.EqualTo(0.5));

            // sample exactly second
            Assert.That(LagCompensation.Sample(history, 2, Interval, out before, out after, out t), Is.True);
            Assert.That(before.value, Is.EqualTo(20));
            Assert.That(after.value, Is.EqualTo(20));
            Assert.That(t, Is.EqualTo(0.0));

            // sample between second and third
            Assert.That(LagCompensation.Sample(history, 2.5, Interval, out before, out after, out t), Is.True);
            Assert.That(before.value, Is.EqualTo(20));
            Assert.That(after.value, Is.EqualTo(30));
            Assert.That(t, Is.EqualTo(0.5));

            // sample exactly third
            Assert.That(LagCompensation.Sample(history, 3, Interval, out before, out after, out t), Is.True);
            Assert.That(before.value, Is.EqualTo(30));
            Assert.That(after.value, Is.EqualTo(30));
            Assert.That(t, Is.EqualTo(0.0));
        }

        [Test]
        public void Sample_Triple_Extrapolate()
        {
            // let's say we capture every 100 ms:
            // 100, 200, 300, 400
            // and the server is at 499
            // if a client sends CmdFire at time 480, then there's no history entry.
            // => adding the current entry every time would be too expensive.
            //    worst case we would capture at 401, 402, 403, 404, ... 100 times
            // => not extrapolating isn't great. low latency clients would be
            //    punished by missing their targets since no entry at 'time' was found.
            // => extrapolation is the best solution. make sure this works as
            //    expected and within limits.

            // insert a few
            LagCompensation.Insert(history, HistoryLimit, 1, new SimpleCapture(1, 10));
            LagCompensation.Insert(history, HistoryLimit, 2, new SimpleCapture(2, 20));
            LagCompensation.Insert(history, HistoryLimit, 3, new SimpleCapture(3, 30));

            // sample at 3.9, just before we capture the next one.
            // this should return before=after=3, with t=1.9.
            // the user can then extrapolate manually.
            Assert.That(LagCompensation.Sample(history, 3.9, Interval, out SimpleCapture before, out SimpleCapture after, out double t), Is.True);
            Assert.That(before.value, Is.EqualTo(20));
            Assert.That(after.value, Is.EqualTo(30));
            Assert.That(t, Is.EqualTo(1.9));

            // exactly interval is still fine
            Assert.That(LagCompensation.Sample(history, 4, Interval, out before, out after, out t), Is.True);
            Assert.That(before.value, Is.EqualTo(20));
            Assert.That(after.value, Is.EqualTo(30));
            Assert.That(t, Is.EqualTo(2.0));

            // it should never extrapolate further than one interval.
            Assert.That(LagCompensation.Sample(history, 4.01, Interval, out before, out after, out t), Is.False);
        }

        [Test]
        public void EstimateTime()
        {
            // server is at  t=100
            // client has an rtt of 80ms
            // snapshot interpolation buffer time is 30ms
            // 100 - 0.080/2 - 0.030 = 99.93
            Assert.That(LagCompensation.EstimateTime(100, 0.080, 0.030), Is.EqualTo(99.93).Within(0.001));
        }
    }
}
