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
        public void TestReadingTooMuch()
        {
            // Try reading more than there is data to be read from
            // The reasoning this should not throw is that when you are running an MMO server
            // you can't expect all your clients to play nice. They can craft any sort of
            // malicious packet, and NetworkReader has to put up with it and take the beating.
            // Currently this test fails with System.IO.EndOfStreamException
            // Ideally NetworkReader should Debug.LogWarning and complain about malformed packets
            // but still continue humming along just fine.
            // MMO Servers cannot afford to be DOS'd and brought to their knees by
            // one salty player with too much computer knowledge.
            Assert.DoesNotThrow(() => {
                Action<NetworkReader>[] readFunctions = new Action<NetworkReader>[]{
                    r => r.ReadByte(),
                    r => r.ReadSByte(),
                    r => r.ReadChar(),
                    r => r.ReadBoolean(),
                    r => r.ReadInt16(),
                    r => r.ReadUInt16(),
                    r => r.ReadInt32(),
                    r => r.ReadUInt32(),
                    r => r.ReadInt64(),
                    r => r.ReadUInt64(),
                    r => r.ReadDecimal(),
                    r => r.ReadSingle(),
                    r => r.ReadDouble(),
                    r => r.ReadString(),
                    r => r.ReadBytes(0),
                    r => r.ReadBytes(1),
                    r => r.ReadBytes(2),
                    r => r.ReadBytes(3),
                    r => r.ReadBytes(4),
                    r => r.ReadBytes(8),
                    r => r.ReadBytes(16),
                    r => r.ReadBytes(32),
                    r => r.ReadBytes(100),
                    r => r.ReadBytes(1000),
                    r => r.ReadBytes(10000),
                    r => r.ReadBytes(1000000),
                    r => r.ReadBytes(10000000),
                    r => r.ReadBytesAndSize(),
                    r => r.ReadPackedInt32(),
                    r => r.ReadPackedUInt32(),
                    r => r.ReadPackedInt64(),
                    r => r.ReadPackedUInt64(),
                    r => r.ReadVector2(),
                    r => r.ReadVector3(),
                    r => r.ReadVector4(),
                    r => r.ReadVector2Int(),
                    r => r.ReadVector3Int(),
                    r => r.ReadColor(),
                    r => r.ReadColor32(),
                    r => r.ReadQuaternion(),
                    r => r.ReadRect(),
                    r => r.ReadPlane(),
                    r => r.ReadRay(),
                    r => r.ReadMatrix4x4(),
                    r => r.ReadGuid(),
                };
                byte[][] readerData = new byte[][]{
                    new byte[] {},
                    new byte[] {0},
                    new byte[] {255,0},
                    new byte[] {255,0,2,3,4,5},
                    new byte[] {255,0,2,3,4,5},
                    new byte[] {255,255,255,255,255,255},
                    new byte[] {250,0},
                    new byte[] {250,0,4,5,6,2},
                    new byte[] {248,2},
                    new byte[] {249,2,2,3,4},
                    new byte[] {1,2,3},
                    new byte[] {1,2,3,4},
                    new byte[] {1,2,3,4,5,6,7},
                    new byte[] {1,2,3,4,5,6,7,8},
                    new byte[] {1,2,3,4,5,6,7,8,9,10,11,12,13,14,15},
                    new byte[] {1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16},
                    new byte[] {1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31},
                    new byte[] {1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,25,26,27,28,29,30,31,32},
                };
                foreach (byte[] data in readerData)
                {
                    foreach (Action<NetworkReader> readFunction in readFunctions)
                    {
                        NetworkReader reader = new NetworkReader(data);
                        readFunction(reader);
                    }
                }
            });
        }

        [Test]
        public void TestReadingInvalidString()
        {
            // These are all bytes which never show up in valid UTF8 encodings.
            // NetworkReader should gracefully handle maliciously crafted input.
            byte[] invalidUTF8bytes = new byte[]
            {
                0xC0, 0xC1, 0xF5, 0xF6,
                0xF7, 0xF8, 0xF9, 0xFA,
                0xFB, 0xFC, 0xFD, 0xFE,
                0xFF,
            };
            Assert.DoesNotThrow(() => {
                foreach (byte invalid in invalidUTF8bytes)
                {
                    NetworkWriter writer = new NetworkWriter();
                    writer.Write("an uncorrupted string");
                    byte[] data = writer.ToArray();
                    data[10] = invalid;
                    NetworkReader reader = new NetworkReader(data);
                    string result = reader.ReadString();
                    Assert.That(result, Is.EqualTo(null));
                }
            });
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
        public void TestUnicodeString()
        {
            string[] weirdUnicode = new string[]{
                "𝔲𝔫𝔦𝔠𝔬𝔡𝔢 𝔱𝔢𝔰𝔱",
                "𝖚𝖓𝖎𝖈𝖔𝖉𝖊 𝖙𝖊𝖘𝖙",
                "𝐮𝐧𝐢𝐜𝐨𝐝𝐞 𝐭𝐞𝐬𝐭",
                "𝘶𝘯𝘪𝘤𝘰𝘥𝘦 𝘵𝘦𝘴𝘵",
                "𝙪𝙣𝙞𝙘𝙤𝙙𝙚 𝙩𝙚𝙨𝙩",
                "𝚞𝚗𝚒𝚌𝚘𝚍𝚎 𝚝𝚎𝚜𝚝",
                "𝓊𝓃𝒾𝒸𝑜𝒹𝑒 𝓉𝑒𝓈𝓉",
                "𝓾𝓷𝓲𝓬𝓸𝓭𝓮 𝓽𝓮𝓼𝓽",
                "𝕦𝕟𝕚𝕔𝕠𝕕𝕖 𝕥𝕖𝕤𝕥",
                "ЦПIᄃӨDΣ ƬΣƧƬ",
                "ㄩ几丨匚ㄖᗪ乇 ㄒ乇丂ㄒ",
                "ひ刀ﾉᄃのり乇 ｲ乇丂ｲ",
                "Ʉ₦ł₵ØĐɆ ₮Ɇ₴₮",
                "ｕｎｉｃｏｄｅ ｔｅｓｔ",
                "ᴜɴɪᴄᴏᴅᴇ ᴛᴇꜱᴛ",
                "ʇsǝʇ ǝpoɔıun",
                "ยภเς๏๔є ՇєรՇ",
                "ᑘᘉᓰᑢᓍᕲᘿ ᖶᘿSᖶ",
                "υɳιƈσԃҽ ƚҽʂƚ",
                "ʊռɨƈօɖɛ ȶɛֆȶ",
                "🆄🅽🅸🅲🅾🅳🅴 🆃🅴🆂🆃",
                "ⓤⓝⓘⓒⓞⓓⓔ ⓣⓔⓢⓣ",
                "̶̝̳̥͈͖̝͌̈͛̽͊̏̚͠",
                // test control codes
                "\r\n", "\n", "\r", "\t",
                "\\", "\"", "\'",
                "\u0000\u0001\u0002\u0003",
                "\u0004\u0005\u0006\u0007",
                "\u0008\u0009\u000A\u000B",
                "\u000C\u000D\u000E\u000F",
                // test invalid bytes as characters
                "\u00C0\u00C1\u00F5\u00F6",
                "\u00F7\u00F8\u00F9\u00FA",
                "\u00FB\u00FC\u00FD\u00FE",
                "\u00FF",
            };
            foreach (string weird in weirdUnicode)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write(weird);
                byte[] data = writer.ToArray();
                NetworkReader reader = new NetworkReader(data);
                string str = reader.ReadString();
                Assert.That(str, Is.EqualTo(weird));
            }
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
        public void TestPackedUInt32Failure()
        {
            Assert.DoesNotThrow(() => {
                NetworkWriter writer = new NetworkWriter();
                writer.WritePackedUInt64(1099511627775);
                writer.WritePackedUInt64(281474976710655);
                writer.WritePackedUInt64(72057594037927935);

                NetworkReader reader = new NetworkReader(writer.ToArray());
                reader.ReadPackedUInt32();
                reader.ReadPackedUInt32();
                reader.ReadPackedUInt32();
            });
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
        public void TestPackedInt32Failure()
        {
            Assert.DoesNotThrow(() => {
                NetworkWriter writer = new NetworkWriter();
                writer.WritePackedInt64(1099511627775);
                writer.WritePackedInt64(281474976710655);
                writer.WritePackedInt64(72057594037927935);

                NetworkReader reader = new NetworkReader(writer.ToArray());
                reader.ReadPackedInt32();
                reader.ReadPackedInt32();
                reader.ReadPackedInt32();
            });
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
        public void TestFloats()
        {
            float[] weirdFloats = new float[]{
                0f,
                -0f,
                float.Epsilon,
                -float.Epsilon,
                float.MaxValue,
                float.MinValue,
                float.NaN,
                -float.NaN,
                float.PositiveInfinity,
                float.NegativeInfinity,
                (float) double.MaxValue,
                (float) double.MinValue,
                (float) decimal.MaxValue,
                (float) decimal.MinValue,
                (float) Math.PI,
                (float) Math.E
            };
            foreach (float weird in weirdFloats)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write(weird);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                float readFloat = reader.ReadSingle();
                Assert.That(readFloat, Is.EqualTo(weird));
            }
        }

        [Test]
        public void TestDoubles()
        {
            double[] weirdDoubles = new double[]{
                0d,
                -0d,
                double.Epsilon,
                -double.Epsilon,
                double.MaxValue,
                double.MinValue,
                double.NaN,
                -double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity,
                float.MaxValue,
                float.MinValue,
                (double) decimal.MaxValue,
                (double) decimal.MinValue,
                Math.PI,
                Math.E
            };
            foreach (double weird in weirdDoubles)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write(weird);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                double readDouble = reader.ReadDouble();
                Assert.That(readDouble, Is.EqualTo(weird));
            }
        }

        [Test]
        public void TestDecimals()
        {
            decimal[] weirdDecimals = new decimal[]{
                decimal.Zero,
                -decimal.Zero,
                decimal.MaxValue,
                decimal.MinValue,
                (decimal) Math.PI,
                (decimal) Math.E
            };
            foreach (decimal weird in weirdDecimals)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write(weird);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                decimal readDecimal = reader.ReadDecimal();
                Assert.That(readDecimal, Is.EqualTo(weird));
            }
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
            Assert.That(reader.ReadString(), Is.Null); // writing null string should write null in Mirror ("" in original HLAPI)
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
