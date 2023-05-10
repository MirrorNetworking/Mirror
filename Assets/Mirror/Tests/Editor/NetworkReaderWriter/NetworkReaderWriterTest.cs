// Test weaver generated Reader/Writer<T>
using NUnit.Framework;

namespace Mirror.Tests.NetworkReaderWriter
{
    [TestFixture]
    public class NetworkReaderWriterTest
    {
        [Test]
        public void TestIntWriterNotNull()
        {
            Assert.That(Writer<int>.write, Is.Not.Null);
        }

        [Test]
        public void TestIntReaderNotNull()
        {
            Assert.That(Reader<int>.read, Is.Not.Null);
        }

        [Test]
        public void TestAccessingCustomWriterAndReader()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.Write(3);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            int copy = reader.Read<int>();

            Assert.That(copy, Is.EqualTo(3));
        }
    }
}
