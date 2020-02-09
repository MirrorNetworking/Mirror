using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{
    public class NetworkWriterPoolTest
    {
        // A Test behaves as an ordinary method
        [Test]
        public void TestPoolRecycling()
        {

            object firstWriter;

            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                firstWriter = writer;
            }

            // Use the Assert class to test conditions
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                Assert.That(writer, Is.SameAs(firstWriter), "Pool should reuse the writer");
            }
        }
    }
}
