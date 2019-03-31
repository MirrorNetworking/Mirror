using System;
using NUnit.Framework;

namespace Mirror.Tests
{
    [TestFixture]
    public class NetworkWriterTest
    {
        [Test]
        public void TestWritingSmallMessage()
        {
            // try serializing less than 32kb and see what happens
            NetworkWriter writer = new NetworkWriter();
            for (int i = 0; i < 30000 / 4; ++i)
                writer.Write(i);
            Assert.That(writer.Position, Is.EqualTo(30000));
        }

        [Test]
        public void TestWritingLargeMessage()
        {
            // try serializing more than 32kb and see what happens
            NetworkWriter writer = new NetworkWriter();
            for (int i = 0; i < 40000 / 4; ++i)
                writer.Write(i);
            Assert.That(writer.Position, Is.EqualTo(40000));
        }

        [Test]
        public void TestWritingHugeArray()
        {
            // try serializing array more than 64KB large and see what happens
            NetworkWriter writer = new NetworkWriter();
            writer.WriteBytesAndSize(new byte[100000]);
            byte[] data = writer.ToArray();

            NetworkReader reader = new NetworkReader(data);
            byte[] deserialized = reader.ReadBytesAndSize();
            Assert.That(deserialized.Length, Is.EqualTo(100000));
        }

        [Test]
        public void TestToArray()
        {
            // write 2 bytes
            NetworkWriter writer = new NetworkWriter();
            writer.Write((byte)1);
            writer.Write((byte)2);

            // .ToArray() length is 2?
            Assert.That(writer.ToArray().Length, Is.EqualTo(2));

            // set position back by one
            writer.Position = 1;

            // Changing the position should not alter the size of the data
            Assert.That(writer.ToArray().Length, Is.EqualTo(2));
        }

        [Test]
        public void TestPackedUInt32()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WritePackedUInt32(0);
            writer.WritePackedUInt32(234);
            writer.WritePackedUInt32(2284);
            writer.WritePackedUInt32(67821);
            writer.WritePackedUInt32(16777210);
            writer.WritePackedUInt32(16777219);
            writer.WritePackedUInt32(uint.MaxValue);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            Assert.That(reader.ReadPackedUInt32(), Is.EqualTo(0));
            Assert.That(reader.ReadPackedUInt32(), Is.EqualTo(234));
            Assert.That(reader.ReadPackedUInt32(), Is.EqualTo(2284));
            Assert.That(reader.ReadPackedUInt32(), Is.EqualTo(67821));
            Assert.That(reader.ReadPackedUInt32(), Is.EqualTo(16777210));
            Assert.That(reader.ReadPackedUInt32(), Is.EqualTo(16777219));
            Assert.That(reader.ReadPackedUInt32(), Is.EqualTo(uint.MaxValue));
        }

        [Test]
        public void TestPackedInt32()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WritePackedInt32(0);
            writer.WritePackedInt32(234);
            writer.WritePackedInt32(2284);
            writer.WritePackedInt32(67821);
            writer.WritePackedInt32(16777210);
            writer.WritePackedInt32(16777219);
            writer.WritePackedInt32(int.MaxValue);
            writer.WritePackedInt32(-1);
            writer.WritePackedInt32(-234);
            writer.WritePackedInt32(-2284);
            writer.WritePackedInt32(-67821);
            writer.WritePackedInt32(-16777210);
            writer.WritePackedInt32(-16777219);
            writer.WritePackedInt32(int.MinValue);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            Assert.That(reader.ReadPackedInt32(), Is.EqualTo(0));
            Assert.That(reader.ReadPackedInt32(), Is.EqualTo(234));
            Assert.That(reader.ReadPackedInt32(), Is.EqualTo(2284));
            Assert.That(reader.ReadPackedInt32(), Is.EqualTo(67821));
            Assert.That(reader.ReadPackedInt32(), Is.EqualTo(16777210));
            Assert.That(reader.ReadPackedInt32(), Is.EqualTo(16777219));
            Assert.That(reader.ReadPackedInt32(), Is.EqualTo(int.MaxValue));
            Assert.That(reader.ReadPackedInt32(), Is.EqualTo(-1));
            Assert.That(reader.ReadPackedInt32(), Is.EqualTo(-234));
            Assert.That(reader.ReadPackedInt32(), Is.EqualTo(-2284));
            Assert.That(reader.ReadPackedInt32(), Is.EqualTo(-67821));
            Assert.That(reader.ReadPackedInt32(), Is.EqualTo(-16777210));
            Assert.That(reader.ReadPackedInt32(), Is.EqualTo(-16777219));
            Assert.That(reader.ReadPackedInt32(), Is.EqualTo(int.MinValue));
        }

        [Test]
        public void TestPackedUInt64()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WritePackedUInt64(0);
            writer.WritePackedUInt64(234);
            writer.WritePackedUInt64(2284);
            writer.WritePackedUInt64(67821);
            writer.WritePackedUInt64(16777210);
            writer.WritePackedUInt64(16777219);
            writer.WritePackedUInt64(4294967295);
            writer.WritePackedUInt64(1099511627775);
            writer.WritePackedUInt64(281474976710655);
            writer.WritePackedUInt64(72057594037927935);
            writer.WritePackedUInt64(ulong.MaxValue);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            Assert.That(reader.ReadPackedUInt64(), Is.EqualTo(0));
            Assert.That(reader.ReadPackedUInt64(), Is.EqualTo(234));
            Assert.That(reader.ReadPackedUInt64(), Is.EqualTo(2284));
            Assert.That(reader.ReadPackedUInt64(), Is.EqualTo(67821));
            Assert.That(reader.ReadPackedUInt64(), Is.EqualTo(16777210));
            Assert.That(reader.ReadPackedUInt64(), Is.EqualTo(16777219));
            Assert.That(reader.ReadPackedUInt64(), Is.EqualTo(4294967295));
            Assert.That(reader.ReadPackedUInt64(), Is.EqualTo(1099511627775));
            Assert.That(reader.ReadPackedUInt64(), Is.EqualTo(281474976710655));
            Assert.That(reader.ReadPackedUInt64(), Is.EqualTo(72057594037927935));
            Assert.That(reader.ReadPackedUInt64(), Is.EqualTo(ulong.MaxValue));
        }

        [Test]
        public void TestPackedInt64()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WritePackedInt64(0);
            writer.WritePackedInt64(234);
            writer.WritePackedInt64(2284);
            writer.WritePackedInt64(67821);
            writer.WritePackedInt64(16777210);
            writer.WritePackedInt64(16777219);
            writer.WritePackedInt64(4294967295);
            writer.WritePackedInt64(1099511627775);
            writer.WritePackedInt64(281474976710655);
            writer.WritePackedInt64(72057594037927935);
            writer.WritePackedInt64(long.MaxValue);
            writer.WritePackedInt64(-1);
            writer.WritePackedInt64(-234);
            writer.WritePackedInt64(-2284);
            writer.WritePackedInt64(-67821);
            writer.WritePackedInt64(-16777210);
            writer.WritePackedInt64(-16777219);
            writer.WritePackedInt64(-4294967295);
            writer.WritePackedInt64(-1099511627775);
            writer.WritePackedInt64(-281474976710655);
            writer.WritePackedInt64(-72057594037927935);
            writer.WritePackedInt64(long.MinValue);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            Assert.That(reader.ReadPackedInt64(), Is.EqualTo(0));
            Assert.That(reader.ReadPackedInt64(), Is.EqualTo(234));
            Assert.That(reader.ReadPackedInt64(), Is.EqualTo(2284));
            Assert.That(reader.ReadPackedInt64(), Is.EqualTo(67821));
            Assert.That(reader.ReadPackedInt64(), Is.EqualTo(16777210));
            Assert.That(reader.ReadPackedInt64(), Is.EqualTo(16777219));
            Assert.That(reader.ReadPackedInt64(), Is.EqualTo(4294967295));
            Assert.That(reader.ReadPackedInt64(), Is.EqualTo(1099511627775));
            Assert.That(reader.ReadPackedInt64(), Is.EqualTo(281474976710655));
            Assert.That(reader.ReadPackedInt64(), Is.EqualTo(72057594037927935));
            Assert.That(reader.ReadPackedInt64(), Is.EqualTo(long.MaxValue));
            Assert.That(reader.ReadPackedInt64(), Is.EqualTo(-1));
            Assert.That(reader.ReadPackedInt64(), Is.EqualTo(-234));
            Assert.That(reader.ReadPackedInt64(), Is.EqualTo(-2284));
            Assert.That(reader.ReadPackedInt64(), Is.EqualTo(-67821));
            Assert.That(reader.ReadPackedInt64(), Is.EqualTo(-16777210));
            Assert.That(reader.ReadPackedInt64(), Is.EqualTo(-16777219));
            Assert.That(reader.ReadPackedInt64(), Is.EqualTo(-4294967295));
            Assert.That(reader.ReadPackedInt64(), Is.EqualTo(-1099511627775));
            Assert.That(reader.ReadPackedInt64(), Is.EqualTo(-281474976710655));
            Assert.That(reader.ReadPackedInt64(), Is.EqualTo(-72057594037927935));
            Assert.That(reader.ReadPackedInt64(), Is.EqualTo(long.MinValue));
        }

        [Test]
        public void TestGuid()
        {
            Guid originalGuid = new Guid("0123456789abcdef9876543210fedcba");
            NetworkWriter writer = new NetworkWriter();
            writer.Write(originalGuid);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            Guid readGuid = reader.ReadGuid();
            Assert.That(readGuid, Is.EqualTo(originalGuid));
        }

        [Test]
        public void TestWritingAndReading()
        {
            // write all simple types once
            NetworkWriter writer = new NetworkWriter();
            writer.Write((char)1);
            writer.Write((byte)2);
            writer.Write((sbyte)3);
            writer.Write(true);
            writer.Write((short)4);
            writer.Write((ushort)5);
            writer.Write(6);
            writer.Write(7U);
            writer.Write(8L);
            writer.Write(9UL);
            writer.Write(10.0F);
            writer.Write(11.0D);
            writer.Write((decimal)12);
            writer.Write((string)null);
            writer.Write("");
            writer.Write("13");
            writer.Write(new byte[] { 14, 15 }, 0, 2); // just the byte array, no size info etc.
            writer.WriteBytesAndSize((byte[])null); // [SyncVar] struct values can have uninitialized byte arrays, null needs to be supported
            writer.WriteBytesAndSize(new byte[] { 17, 18 }, 0, 2); // buffer, no-offset, count
            writer.WriteBytesAndSize(new byte[] { 19, 20, 21 }, 1, 2); // buffer, offset, count
            writer.WriteBytesAndSize(new byte[] { 22, 23 }, 0, 2); // size, buffer

            // read them
            NetworkReader reader = new NetworkReader(writer.ToArray());

            Assert.That(reader.ReadChar(), Is.EqualTo(1));
            Assert.That(reader.ReadByte(), Is.EqualTo(2));
            Assert.That(reader.ReadSByte(), Is.EqualTo(3));
            Assert.That(reader.ReadBoolean(), Is.True);
            Assert.That(reader.ReadInt16(), Is.EqualTo(4));
            Assert.That(reader.ReadUInt16(), Is.EqualTo(5));
            Assert.That(reader.ReadInt32(), Is.EqualTo(6));
            Assert.That(reader.ReadUInt32(), Is.EqualTo(7));
            Assert.That(reader.ReadInt64(), Is.EqualTo(8));
            Assert.That(reader.ReadUInt64(), Is.EqualTo(9));
            Assert.That(reader.ReadSingle(), Is.EqualTo(10));
            Assert.That(reader.ReadDouble(), Is.EqualTo(11));
            Assert.That(reader.ReadDecimal(), Is.EqualTo(12));
            Assert.That(reader.ReadString(), Is.Null); // writing null string should write null in HLAPI Pro ("" in original HLAPI)
            Assert.That(reader.ReadString(), Is.EqualTo(""));
            Assert.That(reader.ReadString(), Is.EqualTo("13"));

            Assert.That(reader.ReadBytes(2), Is.EqualTo(new byte[] { 14, 15 }));

            Assert.That(reader.ReadBytesAndSize(), Is.Null);

            Assert.That(reader.ReadBytesAndSize(), Is.EqualTo(new byte[] { 17, 18 }));

            Assert.That(reader.ReadBytesAndSize(), Is.EqualTo(new byte[] { 20, 21 }));

            Assert.That(reader.ReadBytesAndSize(), Is.EqualTo(new byte[] { 22, 23 }));
        }
    }
}
