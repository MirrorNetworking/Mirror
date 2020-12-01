using System;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class NetworkWriterCollectionTest
    {
        [Test]
        public void HasWriteFunctionForInt()
        {
            Assert.That(Writer<int>.write, Is.Not.Null, "int write function was not found");

            Action<NetworkWriter, int> action = NetworkWriterExtensions.WriteInt32;
            Assert.That(Writer<int>.write, Is.EqualTo(action), "int write function was incorrect value");
        }

        [Test]
        public void HasReadFunctionForInt()
        {
            Assert.That(Reader<int>.read, Is.Not.Null, "int read function was not found");

            Func<NetworkReader, int> action = NetworkReaderExtensions.ReadInt32;
            Assert.That(Reader<int>.read, Is.EqualTo(action), "int read function was incorrect value");
        }
    }
}
