using NUnit.Framework;
using System.Collections.Generic;

namespace Mirror.Tests
{
    // a simple snapshot with timestamp & interpolation
    struct SimpleCapture : Capture
    {
        public double timestamp;
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
    }
}
