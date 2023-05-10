using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace Mirror.Tests.NetworkReaderWriter
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
        public void SetBuffer()
        {
            // start with an initial buffer
            byte[] firstBuffer = {0xFF};
            NetworkReader reader = new NetworkReader(firstBuffer);

            // read one byte so we modify position
            reader.ReadByte();

            // set another buffer
            byte[] secondBuffer = {0x42};
            reader.SetBuffer(secondBuffer);

            // was position reset?
            Assert.That(reader.Position, Is.EqualTo(0));

            // are we really on the second buffer now?
            Assert.That(reader.ReadByte(), Is.EqualTo(0x42));
        }

        [Test]
        public void Remaining()
        {
            byte[] bytes = {0x00, 0x01};
            NetworkReader reader = new NetworkReader(bytes);
            Assert.That(reader.Remaining, Is.EqualTo(2));

            reader.ReadByte();
            Assert.That(reader.Remaining, Is.EqualTo(1));

            reader.ReadByte();
            Assert.That(reader.Remaining, Is.EqualTo(0));
        }

        [Test]
        public void ReadBytes_CountTooBig()
        {
            // calling ReadBytes with a count bigger than what is in Reader
            // should throw an exception
            byte[] bytes = {0x00, 0x01};
            NetworkReader reader = new NetworkReader(bytes);
            Assert.Throws<EndOfStreamException>(() =>
            {
                byte[] result = reader.ReadBytes(bytes, bytes.Length + 1);
            });
        }

        // a user might call ReadBytes(ReadInt()) without verifying size.
        // ReadBytes behaviour with negative count needs to be clearly defined.
        [Test]
        public void ReadBytes_CountNegative()
        {
            // calling ReadBytes with a count bigger than what is in Reader
            // should throw an exception
            byte[] bytes = {0xFF, 0xDD, 0xCC, 0xBB, 0xAA};
            NetworkReader reader = new NetworkReader(bytes);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                byte[] result = reader.ReadBytes(bytes, -1);
            });
        }

        // a user might call ReadBytes(ReadInt()) without verifying size.
        // ReadBytes behaviour with negative count needs to be clearly defined.
        [Test]
        public void ReadBytesSegment_CountNegative()
        {
            // calling ReadBytes with a count bigger than what is in Reader
            // should throw an exception
            byte[] bytes = {0xFF, 0xDD, 0xCC, 0xBB, 0xAA};
            NetworkReader reader = new NetworkReader(bytes);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                ArraySegment<byte> result = reader.ReadBytesSegment(-1);
            });
        }

        // an attacker might try to send invalid utf to throw exceptions.
        // bytes 192, 193, and 245-255 will throw:
        //
        // System.Text.DecoderFallbackException : Unable to translate bytes [C0]
        // at index 0 from specified code page to Unicode.
        //
        // need to ensure the code never throws. simply return "" if invalid.
        [Test]
        public void ReadString_InvalidUTF8()
        {
            byte[] data =
            {
                0x03, 0x00,                              // size header for 2 bytes (it's always +1)
                0x61,                                    // "a"
                192,                                     // invalid byte
                0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x00 // fixed size worth
            };
            NetworkReader reader = new NetworkReader(data);
            Assert.Throws<DecoderFallbackException>(() =>
            {
                string value = reader.ReadString();
            });
        }

        // NetworkReader.ToString actually contained a bug once.
        // ensure this never happens again.
        [Test]
        public void ToStringTest()
        {
            // byte[] with offset and count that's smaller than total length
            byte[] data = {0xA1, 0xB2, 0xC3, 0xD4, 0xE5};
            ArraySegment<byte> segment = new ArraySegment<byte>(data, 1, 3);
            NetworkReader reader = new NetworkReader(segment);
            Assert.That(reader.ToString(), Is.EqualTo("[B2-C3-D4 @ 0/3]"));

            // different position
            reader.Position = 1;
            Assert.That(reader.ToString(), Is.EqualTo("[B2-C3-D4 @ 1/3]"));

            // byte[] with no offset and exact count
            NetworkReader reader2 = new NetworkReader(data);
            Assert.That(reader2.ToString(), Is.EqualTo("[A1-B2-C3-D4-E5 @ 0/5]"));

            // different position
            reader2.Position = 1;
            Assert.That(reader2.ToString(), Is.EqualTo("[A1-B2-C3-D4-E5 @ 1/5]"));
        }
    }
}
