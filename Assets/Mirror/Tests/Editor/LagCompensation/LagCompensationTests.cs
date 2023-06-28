using NUnit.Framework;
using System.Collections.Generic;

namespace Mirror.Tests
{
    // a simple snapshot with timestamp & interpolation
    struct SimpleCapture : Capture
    {
        public int value;

        public SimpleCapture(int value)
        {
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
            LagCompensation.Insert(history, HistoryLimit, 1, new SimpleCapture(10));
            LagCompensation.Insert(history, HistoryLimit, 2, new SimpleCapture(20));
            LagCompensation.Insert(history, HistoryLimit, 3, new SimpleCapture(30));

            Assert.That(history.Count, Is.EqualTo(3));
            Assert.That(history[0].Key, Is.EqualTo(1));
            Assert.That(history[1].Key, Is.EqualTo(2));
            Assert.That(history[2].Key, Is.EqualTo(3));
            Assert.That(history[0].Value.value, Is.EqualTo(10));
            Assert.That(history[1].Value.value, Is.EqualTo(20));
            Assert.That(history[2].Value.value, Is.EqualTo(30));

            // inserting more than limit, should evict the oldest one
            LagCompensation.Insert(history, HistoryLimit, 4, new SimpleCapture(40));
            LagCompensation.Insert(history, HistoryLimit, 5, new SimpleCapture(50));

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
            Assert.That(LagCompensation.Sample(history, 0, out _, out _), Is.False);
        }

        [Test]
        public void Sample_Single()
        {
            // insert a few
            LagCompensation.Insert(history, HistoryLimit, 1, new SimpleCapture(10));

            // sample older than first
            Assert.That(LagCompensation.Sample(history, 0, out SimpleCapture before, out SimpleCapture after), Is.False);

            // sample exactly first
            Assert.That(LagCompensation.Sample(history, 1, out before, out after), Is.True);
            Assert.That(before.value, Is.EqualTo(10));
            Assert.That(after.value, Is.EqualTo(10));

            // sample older than first
            Assert.That(LagCompensation.Sample(history, 2, out before, out after), Is.False);
        }

        [Test]
        public void Sample()
        {
            // insert a few
            LagCompensation.Insert(history, HistoryLimit, 1, new SimpleCapture(10));
            LagCompensation.Insert(history, HistoryLimit, 2, new SimpleCapture(20));
            LagCompensation.Insert(history, HistoryLimit, 3, new SimpleCapture(30));

            // sample older than first
            Assert.That(LagCompensation.Sample(history, 0, out SimpleCapture before, out SimpleCapture after), Is.False);

            // sample exactly first
            Assert.That(LagCompensation.Sample(history, 1, out before, out after), Is.True);
            Assert.That(before.value, Is.EqualTo(10));
            Assert.That(after.value, Is.EqualTo(10));

            // sample between first and second
            Assert.That(LagCompensation.Sample(history, 1.5, out before, out after), Is.True);
            Assert.That(before.value, Is.EqualTo(10));
            Assert.That(after.value, Is.EqualTo(20));

            // sample exactly second
            Assert.That(LagCompensation.Sample(history, 2, out before, out after), Is.True);
            Assert.That(before.value, Is.EqualTo(20));
            Assert.That(after.value, Is.EqualTo(20));

            // sample between second and third
            Assert.That(LagCompensation.Sample(history, 2.5, out before, out after), Is.True);
            Assert.That(before.value, Is.EqualTo(20));
            Assert.That(after.value, Is.EqualTo(30));

            // sample exactly third
            Assert.That(LagCompensation.Sample(history, 3, out before, out after), Is.True);
            Assert.That(before.value, Is.EqualTo(30));
            Assert.That(after.value, Is.EqualTo(30));

            // sample older than third
            Assert.That(LagCompensation.Sample(history, 4, out before, out after), Is.False);
        }
    }
}
