using NUnit.Framework;
using System;
using UnityEngine;

namespace UnityEngine.Networking.Tests
{
    [TestFixture()]
    public class NetworkWriterTest
    {

        [Test]
        public void TestWritingSmallMessage()
        {
            // try serializing <32kb and see what happens
            NetworkWriter writer = new NetworkWriter();
            for (int i = 0; i < 30000 / 4; ++i)
                writer.Write(i);
            Assert.That(writer.Position, Is.EqualTo(30000));
        }

        [Test]
        public void TestWritingLargeMessage()
        {
            // try serializing <32kb and see what happens
            NetworkWriter writer = new NetworkWriter();
            for (int i = 0; i < 40000 / 4; ++i)
                writer.Write(i);
            Assert.That(writer.Position, Is.EqualTo(40000));
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

            // .ToArray() length is 1, even though the internal array contains 2 bytes?
            // (see .ToArray() function comments)
            Assert.That(writer.ToArray().Length, Is.EqualTo(1));
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
            writer.WritePackedUInt32(UInt32.MaxValue);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            Assert.That(reader.ReadPackedUInt32(), Is.EqualTo(0));
            Assert.That(reader.ReadPackedUInt32(), Is.EqualTo(234));
            Assert.That(reader.ReadPackedUInt32(), Is.EqualTo(2284));
            Assert.That(reader.ReadPackedUInt32(), Is.EqualTo(67821));
            Assert.That(reader.ReadPackedUInt32(), Is.EqualTo(16777210));
            Assert.That(reader.ReadPackedUInt32(), Is.EqualTo(16777219));
            Assert.That(reader.ReadPackedUInt32(), Is.EqualTo(UInt32.MaxValue));
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
            writer.WritePackedUInt64(UInt64.MaxValue);

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
            Assert.That(reader.ReadPackedUInt64(), Is.EqualTo(UInt64.MaxValue));
        }

        [Test]
        public void TestWritingAndReading()
        {
            // write all simple types once
            NetworkWriter writer = new NetworkWriter();
            writer.Write((char)1);
            writer.Write((byte)2);
            writer.Write((sbyte)3);
            writer.Write((bool)true);
            writer.Write((short)4);
            writer.Write((ushort)5);
            writer.Write((int)6);
            writer.Write((uint)7);
            writer.Write((long)8L);
            writer.Write((ulong)9L);
            writer.Write((float)10);
            writer.Write((double)11);
            writer.Write((decimal)12);
            writer.Write((string)null);
            writer.Write((string)"");
            writer.Write((string)"13");
            writer.Write(new byte[] { 14, 15 }, 0, 2); // just the byte array, no size info etc.
            writer.WriteBytesAndSize((byte[])null); // [SyncVar] struct values can have uninitialized byte arrays, null needs to be supported
            writer.WriteBytesAndSize(new byte[] { 17, 18 }, 0, 2); // buffer, no-offset, count
            writer.WriteBytesAndSize(new byte[] { 19, 20, 21 }, 1, 2); // buffer, offset, count
            writer.WriteBytesAndSize(new byte[] { 22, 23 }, 0, 2); // size, buffer

            byte[] data = writer.ToArray();


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

            reader.SeekZero();
            Assert.That(reader.Position, Is.Zero);
        }

        [Test]
        public void TestWritingAndReadingCoupleBooleans()
        {
            // write three booleans
            NetworkWriter writer = new NetworkWriter();
            writer.Write(true);
            writer.Write(false);
            writer.Write(true);
            writer.Write((byte)2);

            // read it back
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Assert.That(reader.ReadBoolean(), Is.True);
            Assert.That(reader.ReadBoolean(), Is.False);
            Assert.That(reader.ReadBoolean(), Is.True);
            Assert.That(reader.ReadByte(), Is.EqualTo(2));
        }

        [Test]
        public void TestWritingAndReadingBytesBooleans()
        {
            // write three booleans
            NetworkWriter writer = new NetworkWriter();
            writer.Write((byte)1);
            writer.Write(false);
            writer.Write((byte)2);
            writer.Write(true);
            writer.Write((byte)3);

            // read it back
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Assert.That(reader.ReadByte(), Is.EqualTo(1));
            Assert.That(reader.ReadBoolean(), Is.False);
            Assert.That(reader.ReadByte(), Is.EqualTo(2));
            Assert.That(reader.ReadBoolean(), Is.True);
            Assert.That(reader.ReadByte(), Is.EqualTo(3));
        }

        [Test]
        public void TestWritingAndReadingLotBooleans()
        {
            // write three booleans
            NetworkWriter writer = new NetworkWriter();
            writer.Write(false);
            writer.Write(true);
            writer.Write(false);
            writer.Write(false);
            writer.Write(true);
            writer.Write(false);
            writer.Write(true);
            writer.Write(true);
            writer.Write(false);

            // read it back
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Assert.That(reader.ReadBoolean(), Is.False);
            Assert.That(reader.ReadBoolean(), Is.True);
            Assert.That(reader.ReadBoolean(), Is.False);
            Assert.That(reader.ReadBoolean(), Is.False);
            Assert.That(reader.ReadBoolean(), Is.True);
            Assert.That(reader.ReadBoolean(), Is.False);
            Assert.That(reader.ReadBoolean(), Is.True);
            Assert.That(reader.ReadBoolean(), Is.True);
            Assert.That(reader.ReadBoolean(), Is.False);
        }
    }
}
