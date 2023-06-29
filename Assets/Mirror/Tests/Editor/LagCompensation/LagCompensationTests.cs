using NUnit.Framework;
using System.Collections.Generic;

namespace Mirror.Tests
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
    }

    public class LagCompensationTests
    {
        // buffer for convenience so we don't have to create it manually each time
        List<KeyValuePair<double, SimpleCapture>> history;

        // some defaults
        const int HistoryLimit = 4;

        [SetUp]
        public void SetUp()
        {
            history = new List<KeyValuePair<double, SimpleCapture>>();
        }

        [Test]
        public void Insert()
        {
            // insert a few
            LagCompensation.Insert(history, HistoryLimit, 1, new SimpleCapture(1, 10));
            LagCompensation.Insert(history, HistoryLimit, 2, new SimpleCapture(2, 20));
            LagCompensation.Insert(history, HistoryLimit, 3, new SimpleCapture(3, 30));

            Assert.That(history.Count, Is.EqualTo(3));
            Assert.That(history[0].Key, Is.EqualTo(1));
            Assert.That(history[1].Key, Is.EqualTo(2));
            Assert.That(history[2].Key, Is.EqualTo(3));
            Assert.That(history[0].Value.value, Is.EqualTo(10));
            Assert.That(history[1].Value.value, Is.EqualTo(20));
            Assert.That(history[2].Value.value, Is.EqualTo(30));

            // inserting more than limit, should evict the oldest one
            LagCompensation.Insert(history, HistoryLimit, 4, new SimpleCapture(4, 40));
            LagCompensation.Insert(history, HistoryLimit, 5, new SimpleCapture(5, 50));

            Assert.That(history.Count, Is.EqualTo(4));
            Assert.That(history[0].Key, Is.EqualTo(2));
            Assert.That(history[1].Key, Is.EqualTo(3));
            Assert.That(history[2].Key, Is.EqualTo(4));
            Assert.That(history[3].Key, Is.EqualTo(5));
            Assert.That(history[0].Value.value, Is.EqualTo(20));
            Assert.That(history[1].Value.value, Is.EqualTo(30));
            Assert.That(history[2].Value.value, Is.EqualTo(40));
            Assert.That(history[3].Value.value, Is.EqualTo(50));
        }

        [Test]
        public void Sample_Empty()
        {
            Assert.That(LagCompensation.Sample(history, 0, out _, out _, out _), Is.False);
        }

        [Test]
        public void Sample_Single()
        {
            // insert a few
            LagCompensation.Insert(history, HistoryLimit, 1, new SimpleCapture(1, 10));

            // sample older than first
            Assert.That(LagCompensation.Sample(history, 0, out SimpleCapture before, out SimpleCapture after, out double t), Is.False);

            // sample exactly first
            Assert.That(LagCompensation.Sample(history, 1, out before, out after, out t), Is.True);
            Assert.That(before.value, Is.EqualTo(10));
            Assert.That(after.value, Is.EqualTo(10));
            Assert.That(t, Is.EqualTo(0.0));

            // sample older than first
            Assert.That(LagCompensation.Sample(history, 2, out before, out after, out t), Is.False);
        }

        [Test]
        public void Sample()
        {
            // insert a few
            LagCompensation.Insert(history, HistoryLimit, 1, new SimpleCapture(1, 10));
            LagCompensation.Insert(history, HistoryLimit, 2, new SimpleCapture(2, 20));
            LagCompensation.Insert(history, HistoryLimit, 3, new SimpleCapture(3, 30));

            // sample older than first
            Assert.That(LagCompensation.Sample(history, 0, out SimpleCapture before, out SimpleCapture after, out double t), Is.False);

            // sample exactly first
            Assert.That(LagCompensation.Sample(history, 1, out before, out after, out t), Is.True);
            Assert.That(before.value, Is.EqualTo(10));
            Assert.That(after.value, Is.EqualTo(10));
            Assert.That(t, Is.EqualTo(0.0));

            // sample between first and second
            Assert.That(LagCompensation.Sample(history, 1.5, out before, out after, out t), Is.True);
            Assert.That(before.value, Is.EqualTo(10));
            Assert.That(after.value, Is.EqualTo(20));
            Assert.That(t, Is.EqualTo(0.5));

            // sample exactly second
            Assert.That(LagCompensation.Sample(history, 2, out before, out after, out t), Is.True);
            Assert.That(before.value, Is.EqualTo(20));
            Assert.That(after.value, Is.EqualTo(20));
            Assert.That(t, Is.EqualTo(0.0));

            // sample between second and third
            Assert.That(LagCompensation.Sample(history, 2.5, out before, out after, out t), Is.True);
            Assert.That(before.value, Is.EqualTo(20));
            Assert.That(after.value, Is.EqualTo(30));
            Assert.That(t, Is.EqualTo(0.5));

            // sample exactly third
            Assert.That(LagCompensation.Sample(history, 3, out before, out after, out t), Is.True);
            Assert.That(before.value, Is.EqualTo(30));
            Assert.That(after.value, Is.EqualTo(30));
            Assert.That(t, Is.EqualTo(0.0));

            // sample older than third
            Assert.That(LagCompensation.Sample(history, 4, out before, out after, out t), Is.False);
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
