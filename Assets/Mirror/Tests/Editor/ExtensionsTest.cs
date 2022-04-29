using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class ExtensionsTest
    {
        // supposed to return same result on all platforms
        [Test]
        public void GetStableHashHode()
        {
            Assert.That("".GetStableHashCode(), Is.EqualTo(23));
            Assert.That("Test".GetStableHashCode(), Is.EqualTo(23844169));
        }

        [Test]
        public void CopyToList()
        {
            List<int> source = new List<int>{1, 2, 3};
            List<int> destination = new List<int>();
            source.CopyTo(destination);
            Assert.That(destination.SequenceEqual(source), Is.True);
        }

        [Test]
        public void SortedSet_Trim()
        {
            SortedSet<int> set = new SortedSet<int>{1, 2, 3, 4, 5};

            // larger than count
            set.Trim(6);
            Assert.That(set.SequenceEqual(new []{1, 2, 3, 4, 5}));

            // exactly count
            set.Trim(5);
            Assert.That(set.SequenceEqual(new []{1, 2, 3, 4, 5}));

            // smaller than count
            set.Trim(3);
            Assert.That(set.SequenceEqual(new []{1, 2, 3}));

            // negative should not deadlock
            set.Trim(-1);
            Assert.That(set.SequenceEqual(new []{1, 2, 3}));

            // zero
            set.Trim(0);
            Assert.That(set.SequenceEqual(new int[0]));
        }
    }
}
