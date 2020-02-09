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
        public void NetworkWriterPoolTestSimplePasses()
        {

            object retrievedWriter;

            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                retrievedWriter = writer;
            }

            // Use the Assert class to test conditions
        }

       
    }
}
