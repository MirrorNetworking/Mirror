using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    // NetworkWriterTest already covers most cases for NetworkReader.
    // only a few are left
    [TestFixture]
    public class NetworkReaderTest
    {
        [Test]
        public void ReadBytesCountTooBigTest()
        {
            // calling ReadBytes with a count bigger than what is in Reader
            // should throw an exception
            byte[] bytes = {0x00, 0x01};
            NetworkReader reader = new NetworkReader(bytes);

            try
            {
                byte[] result = reader.ReadBytes(bytes, bytes.Length + 1);
                // BAD: IF WE GOT HERE, THEN NO EXCEPTION WAS THROWN
                Assert.Fail();
            }
            catch (EndOfStreamException)
            {
                // GOOD
            }
        }
    }
}
