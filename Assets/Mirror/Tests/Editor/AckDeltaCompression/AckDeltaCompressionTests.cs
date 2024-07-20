using System.Collections.Generic;
using NUnit.Framework;

namespace Mirror.Tests.AckDeltaCompressionTests
{
    public class AckDeltaCompressionTests
    {
        [Test]
        public void InsertAndAggregate()
        {
            SortedList<double, (ulong, ulong)> history = new SortedList<double, (ulong, ulong)>();
            int MaxCount = 3;

            // insert t = 1 with two dirty bits set.
            // history[1] should simply be the inserted one.
            AckDeltaCompression.InsertAndAggregate(history, 1.0, 0b0001, 0b1000, MaxCount);
            Assert.That(history.Count, Is.EqualTo(1));
            Assert.That(history[1.0], Is.EqualTo(((ulong)0b0001, (ulong)0b1000))); // inserted

            // insert t = 2 with two different dirty bits set.
            // history[1] should be history[1] | inserted.
            // history[2] should be the inserted one.
            AckDeltaCompression.InsertAndAggregate(history, 2.0, 0b0010, 0b0100, MaxCount);
            Assert.That(history.Count, Is.EqualTo(2));
            Assert.That(history[1.0], Is.EqualTo(((ulong)0b0011, (ulong)0b1100))); // [1] | inserted
            Assert.That(history[2.0], Is.EqualTo(((ulong)0b0010, (ulong)0b0100))); // inserted

            // insert t = 3 with one different and one same dirty bit set.
            // history[1] should be history[1] | inserted.
            // history[2] should be history[2] | inserted.
            // history[3] should be the inserted one.
            AckDeltaCompression.InsertAndAggregate(history, 3.0, 0b0100, 0b0100, MaxCount);
            Assert.That(history.Count, Is.EqualTo(3));
            Assert.That(history[1.0], Is.EqualTo(((ulong)0b0111, (ulong)0b1100))); // [1] | inserted
            Assert.That(history[2.0], Is.EqualTo(((ulong)0b0110, (ulong)0b0100))); // [2] | inserted
            Assert.That(history[3.0], Is.EqualTo(((ulong)0b0100, (ulong)0b0100))); // inserted

            // insert t= 4 with two different dirty bits set.
            // should respect max count and remove the first one.
            AckDeltaCompression.InsertAndAggregate(history, 4.0, 0b1000, 0b0010, MaxCount);
            Assert.That(history.Count, Is.EqualTo(3));
            Assert.That(history[2.0], Is.EqualTo(((ulong)0b1110, (ulong)0b0110))); // [2] | inserted
            Assert.That(history[3.0], Is.EqualTo(((ulong)0b1100, (ulong)0b0110))); // [3] | inserted
            Assert.That(history[4.0], Is.EqualTo(((ulong)0b1000, (ulong)0b0010))); // inserted
        }

        [Test]
        public void TrackIdentityAtTick()
        {
            SortedList<double, HashSet<uint>> identityTicks = new SortedList<double, HashSet<uint>>();
            int MaxCount = 3;

            // insert t = 1 with a few netIds
            AckDeltaCompression.TrackIdentityAtTick(1.0, 42, identityTicks, MaxCount);
            AckDeltaCompression.TrackIdentityAtTick(1.0, 1337, identityTicks, MaxCount);
            Assert.That(identityTicks.Count, Is.EqualTo(1)); // t=1
            Assert.That(identityTicks[1.0].Count, Is.EqualTo(2)); // 42, 1337
            Assert.That(identityTicks[1.0].Contains(42));
            Assert.That(identityTicks[1.0].Contains(1337));

            // insert t = 2 with a the same netIds and one new
            AckDeltaCompression.TrackIdentityAtTick(2.0, 42, identityTicks, MaxCount);
            AckDeltaCompression.TrackIdentityAtTick(2.0, 1337, identityTicks, MaxCount);
            AckDeltaCompression.TrackIdentityAtTick(2.0, 101, identityTicks, MaxCount);
            Assert.That(identityTicks.Count, Is.EqualTo(2)); // t=1;2
            Assert.That(identityTicks[1.0].Count, Is.EqualTo(2)); // 42, 1337
            Assert.That(identityTicks[2.0].Count, Is.EqualTo(3)); // 42, 1337, 101
            Assert.That(identityTicks[2.0].Contains(42));
            Assert.That(identityTicks[2.0].Contains(1337));
            Assert.That(identityTicks[2.0].Contains(101));

            // insert t = 3 without one of the previous netIds
            AckDeltaCompression.TrackIdentityAtTick(3.0, 1337, identityTicks, MaxCount);
            AckDeltaCompression.TrackIdentityAtTick(3.0, 101, identityTicks, MaxCount);
            Assert.That(identityTicks.Count, Is.EqualTo(3)); // t=1;2;3
            Assert.That(identityTicks[1.0].Count, Is.EqualTo(2)); // 42, 1337
            Assert.That(identityTicks[2.0].Count, Is.EqualTo(3)); // 42, 1337, 101
            Assert.That(identityTicks[3.0].Count, Is.EqualTo(2)); // 1337, 101
            Assert.That(identityTicks[3.0].Contains(1337));
            Assert.That(identityTicks[3.0].Contains(101));

            // insert t = 4 with the same netIds.
            // this should remove the entries at t=1.
            AckDeltaCompression.TrackIdentityAtTick(4.0, 1337, identityTicks, MaxCount);
            AckDeltaCompression.TrackIdentityAtTick(4.0, 101, identityTicks, MaxCount);
            Assert.That(identityTicks.Count, Is.EqualTo(3)); // t=2;3;4
            Assert.That(identityTicks[2.0].Count, Is.EqualTo(3)); // 42, 1337, 101
            Assert.That(identityTicks[3.0].Count, Is.EqualTo(2)); // 1337, 101
            Assert.That(identityTicks[4.0].Count, Is.EqualTo(2)); // 1337, 101
            Assert.That(identityTicks[4.0].Contains(1337));
            Assert.That(identityTicks[4.0].Contains(101));
        }
    }
}
