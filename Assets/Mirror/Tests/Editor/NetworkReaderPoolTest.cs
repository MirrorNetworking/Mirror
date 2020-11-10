using System;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class NetworkReaderPoolTest
    {
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
    }
}
