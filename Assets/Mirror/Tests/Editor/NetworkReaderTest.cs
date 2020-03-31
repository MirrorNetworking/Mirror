using System.IO;
using NUnit.Framework;

namespace Mirror.Tests
{
    // NetworkWriterTest already covers most cases for NetworkReader.
    // only a few are left
    [TestFixture]
    public class NetworkReaderTest
    {
        /* uncomment if needed. commented for faster test workflow. this takes >3s.
        [Test]
        public void Benchmark()
        {
            // 10 million reads, Unity 2019.3, code coverage disabled
            //   4049ms (+GC later)
            byte[] bytes = { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C };
            for (int i = 0; i < 10000000; ++i)
            {
                ArraySegment<byte> segment = new ArraySegment<byte>(bytes);
                NetworkReader reader = new NetworkReader(segment);
                Vector3 value = reader.ReadVector3();
            }
        }
        */

        [Test]
        public void ReadBytesCountTooBigTest()
        {
            // calling ReadBytes with a count bigger than what is in Reader
            // should throw an exception
            byte[] bytes = { 0x00, 0x01 };

            using (PooledNetworkReader reader = NetworkReaderPool.GetReader(bytes))
            {
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
}
