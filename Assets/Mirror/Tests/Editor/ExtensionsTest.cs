using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class ExtensionsTest
    {
        [Test]
        public void CopyToList()
        {
            List<int> source = new List<int>{1, 2, 3};
            List<int> destination = new List<int>();
            source.CopyTo(destination);
            Assert.That(destination.SequenceEqual(source), Is.True);
        }
    }
}
