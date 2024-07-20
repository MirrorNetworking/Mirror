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
    }
}
