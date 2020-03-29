using System.Linq;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class NetworkWriterPoolTest
    {
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
            PooledNetworkWriter[] writers = new PooledNetworkWriter[testWriterCount];

            for (int i = 0; i < testWriterCount; i++)
            {
                writers[i] = NetworkWriterPool.GetWriter();
            }

            // Make sure all writers are different
            Assert.That(writers.Distinct().Count(), Is.EqualTo(testWriterCount));

            NetworkWriterPool.ResetCapacity();
        }

        [Test]
        [Ignore("WIP")]
        public void PoolReUsesWritersUpToSizeLimit()
        {
            //NetworkWriterPool.ResizePool(5);

            //const int testWriterCount = 5;
            //PooledNetworkWriter[] writers1 = new PooledNetworkWriter[testWriterCount];
            //PooledNetworkWriter[] writers2 = new PooledNetworkWriter[testWriterCount];
            //PooledNetworkWriter extra1;
            //PooledNetworkWriter extra2;

            //// simulate first update
            //for (int i = 0; i < testWriterCount; i++)
            //{
            //    writers1[i] = NetworkWriterPool.GetWriter();
            //}
            //extra1 = NetworkWriterPool.GetWriter();

            //for (int i = 0; i < testWriterCount; i++)
            //{
            //    writers1[i].Dispose();
            //}
            //extra1.Dispose();


            //// simulate second update
            //for (int i = 0; i < testWriterCount; i++)
            //{
            //    writers2[i] = NetworkWriterPool.GetWriter();
            //}
            //extra2 = NetworkWriterPool.GetWriter();

            //for (int i = 0; i < testWriterCount; i++)
            //{
            //    writers2[i].Dispose();
            //}
            //extra2.Dispose();


            //Assert


            //NetworkWriterPool.ResetPoolSize();
        }
    }
}
