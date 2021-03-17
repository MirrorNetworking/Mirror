using Mirror.Experimental;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class SnapshotBufferTests
    {
        SnapshotBuffer buffer;

        [SetUp]
        public void SetUp()
        {
            buffer = new SnapshotBuffer();
        }

        [Test]
        public void Enqueue()
        {
            buffer.Enqueue(new Snapshot());
            Assert.That(buffer.Count, Is.EqualTo(1));
        }

        [Test]
        public void Dequeue_NotOldEnough()
        {
            // add snapshot at time=1
            Snapshot snapshot = new Snapshot{timestamp = 1};
            buffer.Enqueue(snapshot);

            // dequeue at time = 2 with buffer time = 1.5
            // in other words, anything older than 0.5 should dequeue (nothing)
            bool result = buffer.DequeueIfOldEnough(2, 1.5f, out Snapshot value);
            Assert.That(result, Is.False);
        }

        [Test]
        public void Dequeue_OldEnough()
        {
            // add snapshot at time=1
            Snapshot snapshot = new Snapshot{timestamp = 1};
            buffer.Enqueue(snapshot);

            // dequeue at time = 2 with buffer time = 0.5
            // in other words, anything older than 1.5 should dequeue
            bool result = buffer.DequeueIfOldEnough(2, 0.5f, out Snapshot value);
            Assert.That(result, Is.True);
            Assert.That(value, Is.EqualTo(snapshot));
        }

        [Test]
        public void Clear()
        {
            buffer.Enqueue(new Snapshot());
            buffer.Clear();
            Assert.That(buffer.Count, Is.EqualTo(0));
        }
    }
}
