using System.Collections.Generic;
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
        public void Insert_Empty()
        {
            buffer.InsertIfNewEnough(new Snapshot());
            Assert.That(buffer.Count, Is.EqualTo(1));
        }

        [Test]
        public void Insert_NotNewEnough()
        {
            // insert snapshot at time = 1
            buffer.InsertIfNewEnough(new Snapshot{timestamp = 1});
            Assert.That(buffer.Count, Is.EqualTo(1));

            // insert snapshot at time = 0.5 (too old, should be dropped)
            buffer.InsertIfNewEnough(new Snapshot{timestamp = 0.5f});
            Assert.That(buffer.Count, Is.EqualTo(1));
        }

        [Test]
        public void Insert_NewEnough()
        {
            // insert snapshot at time = 1
            buffer.InsertIfNewEnough(new Snapshot{timestamp = 1});
            Assert.That(buffer.Count, Is.EqualTo(1));

            // insert snapshot at time = 1.5 (newer than first = ok)
            buffer.InsertIfNewEnough(new Snapshot{timestamp = 1.5f});
            Assert.That(buffer.Count, Is.EqualTo(2));
        }

        [Test]
        public void Insert_Inbetween()
        {
            // insert snapshot at time = 1
            Snapshot first = new Snapshot{timestamp = 1};
            buffer.InsertIfNewEnough(first);

            // insert snapshot at time = 2
            Snapshot last = new Snapshot{timestamp = 2};
            buffer.InsertIfNewEnough(last);

            // insert snapshot at time = 1.5 (inbetween)
            Snapshot between = new Snapshot{timestamp = 1.5f};
            buffer.InsertIfNewEnough(between);

            // check if sorted properly
            IList<Snapshot> all = buffer.All();
            Assert.That(all.Count, Is.EqualTo(3));
            Assert.That(all[0], Is.EqualTo(first));
            Assert.That(all[1], Is.EqualTo(between));
            Assert.That(all[2], Is.EqualTo(last));
        }

        [Test]
        public void Dequeue_NotOldEnough()
        {
            // add snapshot at time=1
            Snapshot snapshot = new Snapshot{timestamp = 1};
            buffer.InsertIfNewEnough(snapshot);

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
            buffer.InsertIfNewEnough(snapshot);

            // dequeue at time = 2 with buffer time = 0.5
            // in other words, anything older than 1.5 should dequeue
            bool result = buffer.DequeueIfOldEnough(2, 0.5f, out Snapshot value);
            Assert.That(result, Is.True);
            Assert.That(value, Is.EqualTo(snapshot));
        }

        [Test]
        public void Clear()
        {
            buffer.InsertIfNewEnough(new Snapshot());
            buffer.Clear();
            Assert.That(buffer.Count, Is.EqualTo(0));
        }
    }
}
