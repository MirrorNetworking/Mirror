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
    }
}
