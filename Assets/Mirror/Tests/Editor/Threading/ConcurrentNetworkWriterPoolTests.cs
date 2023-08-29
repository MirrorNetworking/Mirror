using NUnit.Framework;
using System.Collections.Generic;

namespace Mirror.Tests
{
    public class ConcurrentNetworkWriterPoolTests
    {
        const int PoolCapacity = ConcurrentNetworkWriterPool.InitialCapacity;

        [SetUp]
        public void SetUp() {}

        [Test]
        public void GetMoreThanCapacity()
        {
            // get more writers than the pool's current capacity
            List<ConcurrentNetworkWriterPooled> writers = new List<ConcurrentNetworkWriterPooled>();
            for (int i = 0; i < PoolCapacity + 1; ++i)
                writers.Add(ConcurrentNetworkWriterPool.Get());

            // return them all
            foreach (ConcurrentNetworkWriterPooled writer in writers)
                ConcurrentNetworkWriterPool.Return(writer);

            // pool should have a larger capacity now
            Assert.That(ConcurrentNetworkWriterPool.Count, Is.EqualTo(PoolCapacity + 1));
        }

        [Test]
        public void Using()
        {
            int startCount = ConcurrentNetworkWriterPool.Count;

            using (ConcurrentNetworkWriterPooled writer = ConcurrentNetworkWriterPool.Get())
            {
                // pool should have one less writer while this one is in use
                Assert.That(ConcurrentNetworkWriterPool.Count, Is.EqualTo(startCount - 1));
            }

            // pool should have the same amount after 'using' returned it
            Assert.That(ConcurrentNetworkWriterPool.Count, Is.EqualTo(startCount));
        }
    }
}
