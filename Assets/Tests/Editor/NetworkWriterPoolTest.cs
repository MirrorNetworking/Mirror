using System.Linq;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class NetworkWriterPoolTest
    {
        int defaultCapacity;

        [SetUp]
        public void SetUp()
        {
            defaultCapacity = NetworkWriterPool.Capacity;
        }

        [TearDown]
        public void TearDown()
        {
            NetworkWriterPool.Capacity = defaultCapacity;
        }

        [Test]
        public void TestPoolRecycling()
        {
            object firstWriter;

            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                firstWriter = writer;
            }

            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                Assert.That(writer, Is.SameAs(firstWriter), "Pool should reuse the writer");
            }
        }

        [Test]
        public void PoolCanGetMoreWritersThanPoolSize()
        {
            NetworkWriterPool.Capacity = 5;

            const int testWriterCount = 10;
            var writers = new PooledNetworkWriter[testWriterCount];

            for (int i = 0; i < testWriterCount; i++)
            {
                writers[i] = NetworkWriterPool.GetWriter();
            }

            // Make sure all writers are different
            Assert.That(writers.Distinct().Count(), Is.EqualTo(testWriterCount));
        }

        [Test]
        public void PoolReUsesWritersUpToSizeLimit()
        {
            NetworkWriterPool.Capacity = 1;

            // get 2 writers
            PooledNetworkWriter a = NetworkWriterPool.GetWriter();
            PooledNetworkWriter b = NetworkWriterPool.GetWriter();

            // recycle all
            NetworkWriterPool.Recycle(a);
            NetworkWriterPool.Recycle(b);

            // get 2 new ones
            PooledNetworkWriter c = NetworkWriterPool.GetWriter();
            PooledNetworkWriter d = NetworkWriterPool.GetWriter();

            // exactly one should be reused, one should be new
            bool cReused = c == a || c == b;
            bool dReused = d == a || d == b;
            Assert.That((cReused && !dReused) ||
                        (!cReused && dReused));
        }

        // if we shrink the capacity, the internal 'next' needs to be adjusted
        // to the new capacity so we don't get a IndexOutOfRangeException
        [Test]
        public void ShrinkCapacity()
        {
            NetworkWriterPool.Capacity = 2;

            // get writer and recycle so we have 2 in there, hence 'next' is at limit
            PooledNetworkWriter a = NetworkWriterPool.GetWriter();
            PooledNetworkWriter b = NetworkWriterPool.GetWriter();
            NetworkWriterPool.Recycle(a);
            NetworkWriterPool.Recycle(b);

            // shrink
            NetworkWriterPool.Capacity = 1;

            // get one. should return the only one which is still in there.
            PooledNetworkWriter c = NetworkWriterPool.GetWriter();
            Assert.That(c, !Is.Null);
            Assert.That(c == a || c == b);
        }

        // if we grow the capacity, things should still work fine
        [Test]
        public void GrowCapacity()
        {
            NetworkWriterPool.Capacity = 1;

            // create and recycle one
            PooledNetworkWriter a = NetworkWriterPool.GetWriter();
            NetworkWriterPool.Recycle(a);

            // grow capacity
            NetworkWriterPool.Capacity = 2;

            // get two
            PooledNetworkWriter b = NetworkWriterPool.GetWriter();
            PooledNetworkWriter c = NetworkWriterPool.GetWriter();
            Assert.That(b, !Is.Null);
            Assert.That(c, !Is.Null);

            // exactly one should be reused, one should be new
            bool bReused = b == a;
            bool cReused = c == a;
            Assert.That((bReused && !cReused) ||
                        (!bReused && cReused));
        }
    }
}
