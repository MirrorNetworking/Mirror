using System;
using System.Linq;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class NetworkReaderPoolTest
    {
        int defaultCapacity;

        [SetUp]
        public void SetUp()
        {
            defaultCapacity = NetworkReaderPool.Capacity;
        }

        [TearDown]
        public void TearDown()
        {
            NetworkReaderPool.Capacity = defaultCapacity;
        }

        [Test]
        public void TestPoolRecycling()
        {
            object firstReader;

            using (PooledNetworkReader Reader = NetworkReaderPool.GetReader(default(ArraySegment<byte>)))
            {
                firstReader = Reader;
            }

            using (PooledNetworkReader Reader = NetworkReaderPool.GetReader(default(ArraySegment<byte>)))
            {
                Assert.That(Reader, Is.SameAs(firstReader), "Pool should reuse the Reader");
            }
        }

        [Test]
        public void PoolCanGetMoreReadersThanPoolSize()
        {
            NetworkReaderPool.Capacity = 5;

            const int testReaderCount = 10;
            PooledNetworkReader[] Readers = new PooledNetworkReader[testReaderCount];

            for (int i = 0; i < testReaderCount; i++)
            {
                Readers[i] = NetworkReaderPool.GetReader(default(ArraySegment<byte>));
            }

            // Make sure all Readers are different
            Assert.That(Readers.Distinct().Count(), Is.EqualTo(testReaderCount));
        }

        [Test]
        public void PoolReUsesReadersUpToSizeLimit()
        {
            NetworkReaderPool.Capacity = 1;

            // get 2 Readers
            PooledNetworkReader a = NetworkReaderPool.GetReader(default(ArraySegment<byte>));
            PooledNetworkReader b = NetworkReaderPool.GetReader(default(ArraySegment<byte>));

            // recycle all
            NetworkReaderPool.Recycle(a);
            NetworkReaderPool.Recycle(b);

            // get 2 new ones
            PooledNetworkReader c = NetworkReaderPool.GetReader(default(ArraySegment<byte>));
            PooledNetworkReader d = NetworkReaderPool.GetReader(default(ArraySegment<byte>));

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
            NetworkReaderPool.Capacity = 2;

            // get Reader and recycle so we have 2 in there, hence 'next' is at limit
            PooledNetworkReader a = NetworkReaderPool.GetReader(default(ArraySegment<byte>));
            PooledNetworkReader b = NetworkReaderPool.GetReader(default(ArraySegment<byte>));
            NetworkReaderPool.Recycle(a);
            NetworkReaderPool.Recycle(b);

            // shrink
            NetworkReaderPool.Capacity = 1;

            // get one. should return the only one which is still in there.
            PooledNetworkReader c = NetworkReaderPool.GetReader(default(ArraySegment<byte>));
            Assert.That(c, !Is.Null);
            Assert.That(c == a || c == b);
        }

        // if we grow the capacity, things should still work fine
        [Test]
        public void GrowCapacity()
        {
            NetworkReaderPool.Capacity = 1;

            // create and recycle one
            PooledNetworkReader a = NetworkReaderPool.GetReader(default(ArraySegment<byte>));
            NetworkReaderPool.Recycle(a);

            // grow capacity
            NetworkReaderPool.Capacity = 2;

            // get two
            PooledNetworkReader b = NetworkReaderPool.GetReader(default(ArraySegment<byte>));
            PooledNetworkReader c = NetworkReaderPool.GetReader(default(ArraySegment<byte>));
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
