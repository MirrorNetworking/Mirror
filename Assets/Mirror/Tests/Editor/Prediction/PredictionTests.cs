using System.Collections.Generic;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class PredictionTests
    {
        [Test]
        public void Sample_Empty()
        {
            // create an empty history
            SortedList<double, int> history = new SortedList<double, int>();

            // sample at 0.5. should fail because empty.
            Assert.That(Prediction.Sample(history, 0.5, out int before, out int after, out int afterIndex, out double t), Is.False);
        }

        [Test]
        public void Sample_OneEntry()
        {
            // create a history with 1 entry
            SortedList<double, int> history = new SortedList<double, int>();
            history.Add(0, 0);

            // sample at 0.5. should fail because sampling always needs to return the _two_ closest ones.
            Assert.That(Prediction.Sample(history, 0.5, out int before, out int after, out int afterIndex, out double t), Is.False);
        }

        [Test]
        public void Sample_BeforeOldest()
        {
            // create a history with 3 entries
            SortedList<double, int> history = new SortedList<double, int>();
            history.Add(0, 0);
            history.Add(1, 1);
            history.Add(2, 2);

            // sample at -1 before the oldest. should fail since there wouldn't be two closest entries around it.
            Assert.That(Prediction.Sample(history, -1, out int before, out int after, out int afterIndex, out double t), Is.False);
        }

        [Test]
        public void Sample_AfterNewest()
        {
            // create a history with 3 entries
            SortedList<double, int> history = new SortedList<double, int>();
            history.Add(0, 0);
            history.Add(1, 1);
            history.Add(2, 2);

            // sample at 3 after the newest. should fail since there wouldn't be two closest entries around it.
            Assert.That(Prediction.Sample(history, 3, out int before, out int after, out int afterIndex, out double t), Is.False);
        }

        [Test]
        public void Sample_MultipleEntries()
        {
            // create a history with 3 entries
            SortedList<double, int> history = new SortedList<double, int>();
            history.Add(0, 0);
            history.Add(1, 1);
            history.Add(2, 2);

            // sample at exactly 0.0: should return '0' twice since it's exactly on that data point.
            Assert.That(Prediction.Sample(history, 0.0, out int before, out int after, out int afterIndex, out double t), Is.True);
            Assert.That(before, Is.EqualTo(0));
            Assert.That(after, Is.EqualTo(0));
            Assert.That(afterIndex, Is.EqualTo(0));
            Assert.That(t, Is.EqualTo(0.0));

            // sample at 0.5
            Assert.That(Prediction.Sample(history, 0.5, out before, out after, out afterIndex, out t), Is.True);
            Assert.That(before, Is.EqualTo(0));
            Assert.That(after, Is.EqualTo(1));
            Assert.That(afterIndex, Is.EqualTo(1));
            Assert.That(t, Is.EqualTo(0.5));

            // sample at 1.5
            Assert.That(Prediction.Sample(history, 1.5, out before, out after, out afterIndex, out t), Is.True);
            Assert.That(before, Is.EqualTo(1));
            Assert.That(after, Is.EqualTo(2));
            Assert.That(afterIndex, Is.EqualTo(2));
            Assert.That(t, Is.EqualTo(0.5));

            // sample at exactly 2.0. should return '2' twice since it's exactly on that data point.
            Assert.That(Prediction.Sample(history, 2.0, out before, out after, out afterIndex, out t), Is.True);
            Assert.That(before, Is.EqualTo(2));
            Assert.That(after, Is.EqualTo(2));
            Assert.That(afterIndex, Is.EqualTo(2));
            Assert.That(t, Is.EqualTo(0.0));
        }
    }
}
