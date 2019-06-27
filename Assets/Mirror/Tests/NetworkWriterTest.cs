using System;
using NUnit.Framework;
using UnityEngine;

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
        public void TestWritingBytesSegment()
        {
            byte[] data = {1, 2, 3};
            NetworkWriter writer = new NetworkWriter();
            writer.Write(data, 0, data.Length);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            ArraySegment<byte> deserialized = reader.ReadBytesSegment(data.Length);
            Assert.That(deserialized.Count, Is.EqualTo(data.Length));
            for (int i = 0; i < data.Length; ++i)
                Assert.That(deserialized.Array[deserialized.Offset + i], Is.EqualTo(data[i]));
        }

        // write byte[], read segment
        [Test]
        public void TestWritingBytesAndReadingSegment()
        {
            byte[] data = {1, 2, 3};
            NetworkWriter writer = new NetworkWriter();
            writer.WriteBytesAndSize(data);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            ArraySegment<byte> deserialized = reader.ReadBytesAndSizeSegment();
            Assert.That(deserialized.Count, Is.EqualTo(data.Length));
            for (int i = 0; i < data.Length; ++i)
                Assert.That(deserialized.Array[deserialized.Offset + i], Is.EqualTo(data[i]));
        }

        // write segment, read segment
        [Test]
        public void TestWritingSegmentAndReadingSegment()
        {
            byte[] data = {1, 2, 3, 4};
            ArraySegment<byte> segment = new ArraySegment<byte>(data, 1, 1); // [2, 3]
            NetworkWriter writer = new NetworkWriter();
            writer.WriteBytesAndSizeSegment(segment);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            ArraySegment<byte> deserialized = reader.ReadBytesAndSizeSegment();
            Assert.That(deserialized.Count, Is.EqualTo(segment.Count));
            for (int i = 0; i < segment.Count; ++i)
                Assert.That(deserialized.Array[deserialized.Offset + i], Is.EqualTo(segment.Array[segment.Offset + i]));
        }

        [Test]
        public void TestOverwritingData()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.Write(Matrix4x4.identity);
            writer.Write(1.23456789m);
            writer.Position += 10;
            writer.Write(Vector3.negativeInfinity);
            writer.Position = 46;
            // write right at the boundary before SetLength
            writer.Write(0xfeed_babe_c0ffee);
            // test that SetLength clears data beyond length
            writer.SetLength(50);
            // check that jumping leaves 0s between
            writer.Position = 100;
            writer.Write("no worries, m8");
            writer.Position = 64;
            writer.Write(true);
            // check that clipping off the end affect ToArray()'s length
            writer.SetLength(128);
            byte[] output = writer.ToArray();
            //Debug.Log(BitConverter.ToString(output));
            byte[] expected = {
                0x00, 0x00, 0x80, 0x3F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x80, 0x3F, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x80, 0x3F, 0x00, 0x00, 0xEE, 0xFF, 0xC0, 0xBE,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x0F, 0x00, 0x6E, 0x6F, 0x20, 0x77, 0x6F, 0x72, 0x72, 0x69,
                0x65, 0x73, 0x2C, 0x20, 0x6D, 0x38, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
            };
            Assert.That(output, Is.EqualTo(expected));
        }

        [Test]
        public void TestSetLengthZeroes()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.Write("I saw");
            writer.Write(0xA_FADED_DEAD_EEL);
            writer.Write("and ate it");
            int position = writer.Position;
            writer.SetLength(10);
            // Setting length should set position too
            Assert.That(writer.Position, Is.EqualTo(10));
            // lets grow it back and check there's zeroes now.
            writer.SetLength(position);
            byte[] data = writer.ToArray();
            for (int i = position; i < data.Length; i++)
                Assert.That(data[i], Is.EqualTo(0), $"index {i} should have value 0");
        }

        [Test]
        public void TestSetLengthInitialization()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.SetLength(10);
            // Setting length should leave position at 0
            Assert.That(writer.Position, Is.EqualTo(0));
            byte[] data = writer.ToArray();
            for (int i = 0; i < data.Length; i++)
                Assert.That(data[i], Is.EqualTo(0), $"index {i} should have value 0");
        }

        [Test]
        public void TestReadingLengthWrapAround()
        {
            NetworkWriter writer = new NetworkWriter();
            // This is 1.5x int.MaxValue, in the negative range of int.
            writer.WritePackedUInt32(3221225472);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Assert.Throws<System.OverflowException>(() => reader.ReadBytesAndSize());
        }

        [Test]
        public void TestReading0LengthBytesAndSize()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteBytesAndSize(new byte[]{});
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Assert.That(reader.ReadBytesAndSize().Length, Is.EqualTo(0));
        }

        [Test]
        public void TestReading0LengthBytes()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.Write(new byte[]{}, 0, 0);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Assert.That(reader.ReadBytes(0).Length, Is.EqualTo(0));
        }

        [Test]
        public void TestWritingNegativeBytesAndSizeFailure()
        {
            NetworkWriter writer = new NetworkWriter();
            Assert.Throws<OverflowException>(() => writer.WriteBytesAndSize(new byte[0], 0, -1));
            Assert.That(writer.Position, Is.EqualTo(0));
        }

        [Test]
        public void TestReadingTooMuch()
        {
            void EnsureThrows(Action<NetworkReader> read, byte[] data = null)
            {
                Assert.Throws<System.IO.EndOfStreamException>(() => read(new NetworkReader(data ?? new byte[]{})));
            }
            // Try reading more than there is data to be read from
            // This should throw EndOfStreamException always
            EnsureThrows(r => r.ReadByte());
            EnsureThrows(r => r.ReadSByte());
            EnsureThrows(r => r.ReadChar());
            EnsureThrows(r => r.ReadBoolean());
            EnsureThrows(r => r.ReadInt16());
            EnsureThrows(r => r.ReadUInt16());
            EnsureThrows(r => r.ReadInt32());
            EnsureThrows(r => r.ReadUInt32());
            EnsureThrows(r => r.ReadInt64());
            EnsureThrows(r => r.ReadUInt64());
            EnsureThrows(r => r.ReadDecimal());
            EnsureThrows(r => r.ReadSingle());
            EnsureThrows(r => r.ReadDouble());
            EnsureThrows(r => r.ReadString());
            EnsureThrows(r => r.ReadBytes(1));
            EnsureThrows(r => r.ReadBytes(2));
            EnsureThrows(r => r.ReadBytes(3));
            EnsureThrows(r => r.ReadBytes(4));
            EnsureThrows(r => r.ReadBytes(8));
            EnsureThrows(r => r.ReadBytes(16));
            EnsureThrows(r => r.ReadBytes(32));
            EnsureThrows(r => r.ReadBytes(100));
            EnsureThrows(r => r.ReadBytes(1000));
            EnsureThrows(r => r.ReadBytes(10000));
            EnsureThrows(r => r.ReadBytes(1000000));
            EnsureThrows(r => r.ReadBytes(10000000));
            EnsureThrows(r => r.ReadBytesAndSize());
            EnsureThrows(r => r.ReadPackedInt32());
            EnsureThrows(r => r.ReadPackedUInt32());
            EnsureThrows(r => r.ReadPackedInt64());
            EnsureThrows(r => r.ReadPackedUInt64());
            EnsureThrows(r => r.ReadVector2());
            EnsureThrows(r => r.ReadVector3());
            EnsureThrows(r => r.ReadVector4());
            EnsureThrows(r => r.ReadVector2Int());
            EnsureThrows(r => r.ReadVector3Int());
            EnsureThrows(r => r.ReadColor());
            EnsureThrows(r => r.ReadColor32());
            EnsureThrows(r => r.ReadQuaternion());
            EnsureThrows(r => r.ReadRect());
            EnsureThrows(r => r.ReadPlane());
            EnsureThrows(r => r.ReadRay());
            EnsureThrows(r => r.ReadMatrix4x4());
            EnsureThrows(r => r.ReadGuid());
        }

        [Test]
        public void TestVector2()
        {
            Vector2[] inputs = new Vector2[]{
                Vector2.right,
                Vector2.up,
                Vector2.zero,
                Vector2.one,
                Vector2.positiveInfinity,
                new Vector2(0.1f,3.1f)
            };
            foreach (Vector2 input in inputs)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Vector2 output = reader.ReadVector2();
                Assert.That(output, Is.EqualTo(input));
            }
        }

        [Test]
        public void TestVector3()
        {
            Vector3[] inputs = new Vector3[]{
                Vector3.right,
                Vector3.up,
                Vector3.zero,
                Vector3.one,
                Vector3.positiveInfinity,
                Vector3.forward,
                new Vector3(0.1f,3.1f,1.4f)
            };
            foreach (Vector3 input in inputs)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Vector3 output = reader.ReadVector3();
                Assert.That(output, Is.EqualTo(input));
            }
        }

        [Test]
        public void TestVector4()
        {
            Vector4[] inputs = new Vector4[]{
                Vector3.right,
                Vector3.up,
                Vector4.zero,
                Vector4.one,
                Vector4.positiveInfinity,
                new Vector4(0.1f,3.1f,1.4f,4.9f)
            };
            foreach (Vector4 input in inputs)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Vector4 output = reader.ReadVector4();
                Assert.That(output, Is.EqualTo(input));
            }
        }

        [Test]
        public void TestVector2Int()
        {
            Vector2Int[] inputs = new Vector2Int[]{
                Vector2Int.down,
                Vector2Int.up,
                Vector2Int.left,
                Vector2Int.zero,
                new Vector2Int(-1023,-999999),
                new Vector2Int(257,12345),
                new Vector2Int(0x7fffffff,-12345),
            };
            foreach (Vector2Int input in inputs)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Vector2Int output = reader.ReadVector2Int();
                Assert.That(output, Is.EqualTo(input));
            }
        }

        [Test]
        public void TestVector3Int()
        {
            Vector3Int[] inputs = new Vector3Int[]{
                Vector3Int.down,
                Vector3Int.up,
                Vector3Int.left,
                Vector3Int.one,
                Vector3Int.zero,
                new Vector3Int(-1023,-999999,1392),
                new Vector3Int(257,12345,-6132),
                new Vector3Int(0x7fffffff,-12345,-1),
            };
            foreach (Vector3Int input in inputs)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Vector3Int output = reader.ReadVector3Int();
                Assert.That(output, Is.EqualTo(input));
            }
        }

        [Test]
        public void TestColor()
        {
            Color[] inputs = new Color[]{
                Color.black,
                Color.blue,
                Color.cyan,
                Color.yellow,
                Color.magenta,
                Color.white,
                new Color(0.401f,0.2f,1.0f,0.123f)
            };
            foreach (Color input in inputs)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Color output = reader.ReadColor();
                Assert.That(output, Is.EqualTo(input));
            }
        }

        [Test]
        public void TestColor32()
        {
            Color32[] inputs = new Color32[]{
                Color.black,
                Color.blue,
                Color.cyan,
                Color.yellow,
                Color.magenta,
                Color.white,
                new Color32(0xab,0xcd,0xef,0x12),
                new Color32(125,126,0,255)
            };
            foreach (Color32 input in inputs)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Color32 output = reader.ReadColor32();
                Assert.That(output, Is.EqualTo(input));
            }
        }

        [Test]
        public void TestQuaternion()
        {
            Quaternion[] inputs = new Quaternion[]{
                Quaternion.identity,
                default,
                Quaternion.LookRotation(new Vector3(0.3f,0.4f,0.5f)),
                Quaternion.Euler(45f,56f,Mathf.PI)
            };
            foreach (Quaternion input in inputs)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Quaternion output = reader.ReadQuaternion();
                Assert.That(output, Is.EqualTo(input));
            }
        }

        [Test]
        public void TestRect()
        {
            Rect[] inputs = new Rect[]{
                Rect.zero,
                new Rect(1004.1f,2.001f,4636,400f),
                new Rect(-100.622f,-200f,300f,975.6f),
                new Rect(-100f,435,-30.04f,400f),
                new Rect(55,-200f,-44,-123),
            };
            foreach (Rect input in inputs)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Rect output = reader.ReadRect();
                Assert.That(output, Is.EqualTo(input));
            }
        }

        [Test]
        public void TestPlane()
        {
            Plane[] inputs = new Plane[]{
                new Plane(new Vector3(-0.24f,0.34f,0.2f), 120.2f),
                new Plane(new Vector3(0.133f,0.34f,0.122f), -10.135f),
                new Plane(new Vector3(0.133f,-0.0f,float.MaxValue), -13.3f),
                new Plane(new Vector3(0.1f,-0.2f,0.3f), 14.5f),
            };
            foreach (Plane input in inputs)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Plane output = reader.ReadPlane();
                // note: Plane constructor does math internally, resulting in
                // floating point precision loss that causes exact comparison
                // to fail the test. So we test that the difference is small.
                Assert.That((output.normal - input.normal).magnitude, Is.LessThan(1e-6f));
                Assert.That(output.distance, Is.EqualTo(input.distance));
            }
        }

        [Test]
        public void TestRay()
        {
            Ray[] inputs = new Ray[]{
                new Ray(Vector3.up,Vector3.down),
                new Ray(new Vector3(0.1f,0.2f,0.3f), new Vector3(0.4f,0.5f,0.6f)),
                new Ray(new Vector3(-0.3f,0.5f,0.999f), new Vector3(1f,100.1f,20f)),
            };
            foreach (Ray input in inputs)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Ray output = reader.ReadRay();
                Assert.That((output.direction - input.direction).magnitude, Is.LessThan(1e-6f));
                Assert.That(output.origin, Is.EqualTo(input.origin));
            }
        }

        [Test]
        public void TestMatrix4x4()
        {
            Matrix4x4[] inputs = new Matrix4x4[]{
                Matrix4x4.identity,
                Matrix4x4.zero,
                Matrix4x4.Scale(Vector3.one * 0.12345f),
                Matrix4x4.LookAt(Vector2.up,Vector3.right,Vector3.forward),
                Matrix4x4.Rotate(Quaternion.LookRotation(Vector3.one)),
            };
            foreach (Matrix4x4 input in inputs)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Matrix4x4 output = reader.ReadMatrix4x4();
                Assert.That(output, Is.EqualTo(input));
            }
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
            foreach (byte invalid in invalidUTF8bytes)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.Write("an uncorrupted string");
                byte[] data = writer.ToArray();
                data[10] = invalid;
                NetworkReader reader = new NetworkReader(data);
                Assert.Throws<System.Text.DecoderFallbackException>(() => reader.ReadString());
            }
        }

        [Test]
        public void TestReadingTruncatedString()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.Write("a string longer than 10 bytes");
            writer.SetLength(10);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Assert.Throws<System.IO.EndOfStreamException>(() => reader.ReadString());
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
        public void TestToArraySegment()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.Write("hello");
            writer.Write("world");

            NetworkReader reader = new NetworkReader(writer.ToArraySegment());
            Assert.That(reader.ReadString(), Is.EqualTo("hello"));
            Assert.That(reader.ReadString(), Is.EqualTo("world"));
        }

        [Test]
        public void TestChar()
        {
            char a = 'a';
            char u = 'ⓤ';

            NetworkWriter writer = new NetworkWriter();
            writer.Write(a);
            writer.Write(u);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            char a2 = reader.ReadChar();
            Assert.That(a2, Is.EqualTo(a));
            char u2 = reader.ReadChar();
            Assert.That(u2, Is.EqualTo(u));
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
            Assert.Throws<System.OverflowException>(() => {
                NetworkWriter writer = new NetworkWriter();
                writer.WritePackedUInt64(1099511627775);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                reader.ReadPackedUInt32();
            });
            Assert.Throws<System.OverflowException>(() => {
                NetworkWriter writer = new NetworkWriter();
                writer.WritePackedUInt64(281474976710655);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                reader.ReadPackedUInt32();
            });
            Assert.Throws<System.OverflowException>(() => {
                NetworkWriter writer = new NetworkWriter();
                writer.WritePackedUInt64(72057594037927935);
                NetworkReader reader = new NetworkReader(writer.ToArray());
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
            Assert.Throws<System.OverflowException>(() => {
                NetworkWriter writer = new NetworkWriter();
                writer.WritePackedInt64(1099511627775);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                reader.ReadPackedInt32();
            });
            Assert.Throws<System.OverflowException>(() => {
                NetworkWriter writer = new NetworkWriter();
                writer.WritePackedInt64(281474976710655);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                reader.ReadPackedInt32();
            });
            Assert.Throws<System.OverflowException>(() => {
                NetworkWriter writer = new NetworkWriter();
                writer.WritePackedInt64(72057594037927935);
                NetworkReader reader = new NetworkReader(writer.ToArray());
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
        public void TestFloatBinaryCompatibility()
        {
            float[] weirdFloats = new float[]{
                ((float) Math.PI) / 3.0f,
                ((float) Math.E) / 3.0f
            };
            byte[] expected = new byte[]{
                146, 10,134, 63,
                197,245,103, 63,
            };
            NetworkWriter writer = new NetworkWriter();
            foreach (float weird in weirdFloats)
            {
                writer.Write(weird);
            }
            Assert.That(writer.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void TestDoubleBinaryCompatibility()
        {
            double[] weirdDoubles = new double[]{
                Math.PI / 3.0d,
                Math.E / 3.0d
            };
            byte[] expected = new byte[]{
                101,115, 45, 56, 82,193,240, 63,
                140,116,112,185,184,254,236, 63,
            };
            NetworkWriter writer = new NetworkWriter();
            foreach (double weird in weirdDoubles)
            {
                writer.Write(weird);
            }
            Assert.That(writer.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void TestDecimalBinaryCompatibility()
        {
            decimal[] weirdDecimals = new decimal[]{
                ((decimal) Math.PI) / 3.0m,
                ((decimal) Math.E) / 3.0m
            };
            byte[] expected = new byte[]{
                0x00, 0x00, 0x1C, 0x00, 0x12, 0x37, 0xD6, 0x21, 0xAB, 0xEA,
                0x84, 0x0A, 0x5B, 0x5E, 0xB1, 0x03, 0x00, 0x00, 0x0E, 0x00,
                0x00, 0x00, 0x00, 0x00, 0xF0, 0x6D, 0xC2, 0xA4, 0x68, 0x52,
                0x00, 0x00
            };
            NetworkWriter writer = new NetworkWriter();
            foreach (decimal weird in weirdDecimals)
            {
                writer.Write(weird);
            }
            //Debug.Log(BitConverter.ToString(writer.ToArray()));
            Assert.That(writer.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void TestByteEndianness()
        {
            byte[] values = new byte[]{0x12,0x43,0x00,0xff,0xab,0x02,0x20};
            byte[] expected = new byte[]{0x12,0x43,0x00,0xff,0xab,0x02,0x20};
            NetworkWriter writer = new NetworkWriter();
            foreach (byte value in values)
            {
                writer.Write(value);
            }
            Assert.That(writer.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void TestUShortEndianness()
        {
            ushort[] values = new ushort[]{0x0000,0x1234,0xabcd,0xF00F,0x0FF0,0xbeef};
            byte[] expected = new byte[]{0x00,0x00,0x34,0x12,0xcd,0xab,0x0F,0xF0,0xF0,0x0F,0xef,0xbe};
            NetworkWriter writer = new NetworkWriter();
            foreach (ushort value in values)
            {
                writer.Write(value);
            }
            Assert.That(writer.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void TestUIntEndianness()
        {
            uint[] values = new uint[]{0x12345678,0xabcdef09,0xdeadbeef};
            byte[] expected = new byte[]{0x78,0x56,0x34,0x12,0x09,0xef,0xcd,0xab,0xef,0xbe,0xad,0xde};
            NetworkWriter writer = new NetworkWriter();
            foreach (uint value in values)
            {
                writer.Write(value);
            }
            Assert.That(writer.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void TestULongEndianness()
        {
            ulong[] values = new ulong[]{0x0123456789abcdef,0xdeaded_beef_c0ffee};
            byte[] expected = new byte[]{0xef,0xcd,0xab,0x89,0x67,0x45,0x23,0x01,0xee,0xff,0xc0,0xef,0xbe,0xed,0xad,0xde};
            NetworkWriter writer = new NetworkWriter();
            foreach (ulong value in values)
            {
                writer.Write(value);
            }
            Assert.That(writer.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void TestSbyteEndianness()
        {
            byte[] values = new byte[]{0x12,0x43,0x00,0xff,0xab,0x02,0x20};
            byte[] expected = new byte[]{0x12,0x43,0x00,0xff,0xab,0x02,0x20};
            NetworkWriter writer = new NetworkWriter();
            foreach (byte value in values)
            {
                writer.Write((sbyte) value);
            }
            Assert.That(writer.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void TestShortEndianness()
        {
            ushort[] values = new ushort[]{0x0000,0x1234,0xabcd,0xF00F,0x0FF0,0xbeef};
            byte[] expected = new byte[]{0x00,0x00,0x34,0x12,0xcd,0xab,0x0F,0xF0,0xF0,0x0F,0xef,0xbe};
            NetworkWriter writer = new NetworkWriter();
            foreach (ushort value in values)
            {
                writer.Write((short) value);
            }
            Assert.That(writer.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void TestIntEndianness()
        {
            uint[] values = new uint[]{0x12345678,0xabcdef09,0xdeadbeef};
            byte[] expected = new byte[]{0x78,0x56,0x34,0x12,0x09,0xef,0xcd,0xab,0xef,0xbe,0xad,0xde};
            NetworkWriter writer = new NetworkWriter();
            foreach (uint value in values)
            {
                writer.Write((int) value);
            }
            Assert.That(writer.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void TestLongEndianness()
        {
            ulong[] values = new ulong[]{0x0123456789abcdef,0xdeaded_beef_c0ffee};
            byte[] expected = new byte[]{0xef,0xcd,0xab,0x89,0x67,0x45,0x23,0x01,0xee,0xff,0xc0,0xef,0xbe,0xed,0xad,0xde};
            NetworkWriter writer = new NetworkWriter();
            foreach (ulong value in values)
            {
                writer.Write((long) value);
            }
            Assert.That(writer.ToArray(), Is.EqualTo(expected));
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
