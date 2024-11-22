using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Mirror.Tests.Rpcs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.NetworkReaderWriter
{
    [TestFixture]
    public class NetworkWriterTest : MirrorEditModeTest
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // start server & connect client because we need spawn functions
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out _);
        }

        /* uncomment if needed. commented for faster test workflow. this takes >3s.
        [Test]
        public void Benchmark()
        {
            // 10 million reads, Unity 2019.3, code coverage disabled
            //    4014ms ms
            NetworkWriter writer = new NetworkWriter();
            for (int i = 0; i < 10000000; ++i)
            {
                writer.SetLength(0);
                writer.WriteVector3(new Vector3(1, 2, 3));
            }
        }
        */

        // Write/ReadBlittable assumes same endianness on server & client.
        [Test]
        public void LittleEndianPlatform()
        {
            Assert.That(BitConverter.IsLittleEndian, Is.True);
        }

        [Test]
        public void ToStringTest()
        {
            NetworkWriter writer = new NetworkWriter();

            writer.WriteUInt(0xAABBCCDD);
            writer.WriteByte(0xFF);
            // should show [content, position / capacity].
            // showing "position / space] is too confusing.
            Assert.That(writer.ToString(), Is.EqualTo($"[DD-CC-BB-AA-FF @ 5/{NetworkWriter.DefaultCapacity}]"));
        }

        [Test]
        public void SegmentImplicit()
        {
            NetworkWriter writer = new NetworkWriter();

            writer.WriteUInt(0xAABBCCDD);
            writer.WriteByte(0xFF);
            ArraySegment<byte> segment = writer;
            Assert.That(segment.SequenceEqual(new byte[] {0xDD, 0xCC, 0xBB, 0xAA, 0xFF}));
        }

        // some platforms may not support unaligned *(T*) reads/writes.
        // but it still needs to work with our workaround.
        // let's have an editor test to maybe catch it early.
        // Editor runs Win/Mac/Linux and atm the issue only exists on Android,
        // but let's have a test anyway.
        // see also: https://github.com/vis2k/Mirror/issues/3044
        [Test]
        public void WriteUnaligned()
        {
            NetworkWriter writer = new NetworkWriter();
            // make unaligned
            writer.WriteByte(0xFF);
            // write a double
            writer.WriteDouble(Math.PI);
            // should have written 9 bytes without throwing exceptions
            Assert.That(writer.Position, Is.EqualTo(9));
        }

        [Test]
        public void TestWritingSmallMessage()
        {
            // try serializing less than 32kb and see what happens
            NetworkWriter writer = new NetworkWriter();
            for (int i = 0; i < 30000 / 4; ++i)
                writer.WriteInt(i);
            Assert.That(writer.Position, Is.EqualTo(30000));
        }

        [Test]
        public void TestWritingLargeMessage()
        {
            // try serializing more than 32kb and see what happens
            NetworkWriter writer = new NetworkWriter();
            for (int i = 0; i < 40000 / 4; ++i)
                writer.WriteInt(i);
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
            byte[] data = { 1, 2, 3 };
            NetworkWriter writer = new NetworkWriter();
            writer.WriteBytes(data, 0, data.Length);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            ArraySegment<byte> deserialized = reader.ReadBytesSegment(data.Length);
            Assert.That(deserialized.Count, Is.EqualTo(data.Length));
            for (int i = 0; i < data.Length; ++i)
                Assert.That(deserialized.Array[deserialized.Offset + i], Is.EqualTo(data[i]));
        }

        [Test]
        public unsafe void WriteBytes_Ptr()
        {
            NetworkWriter writer = new NetworkWriter();

            // make sure this respects position & offset
            writer.WriteByte(0xFF);

            byte[] bytes = {0x01, 0x02, 0x03, 0x04};
            fixed (byte* ptr = bytes)
            {
                Assert.True(writer.WriteBytes(ptr, 1, 2));
                Assert.True(writer.ToArraySegment().SequenceEqual(new byte[] {0xFF, 0x02, 0x03}));
            }
        }

        // write byte[], read segment
        [Test]
        public void TestWritingBytesAndReadingSegment()
        {
            byte[] data = { 1, 2, 3 };
            NetworkWriter writer = new NetworkWriter();
            writer.WriteBytesAndSize(data);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            ArraySegment<byte> deserialized = reader.ReadArraySegmentAndSize();
            Assert.That(deserialized.Count, Is.EqualTo(data.Length));
            for (int i = 0; i < data.Length; ++i)
                Assert.That(deserialized.Array[deserialized.Offset + i], Is.EqualTo(data[i]));
        }

        // write segment, read segment
        [Test]
        public void TestWritingSegmentAndReadingSegment()
        {
            byte[] data = { 1, 2, 3, 4 };
            // [2, 3]
            ArraySegment<byte> segment = new ArraySegment<byte>(data, 1, 1);
            NetworkWriter writer = new NetworkWriter();
            writer.WriteArraySegmentAndSize(segment);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            ArraySegment<byte> deserialized = reader.ReadArraySegmentAndSize();
            Assert.That(deserialized.Count, Is.EqualTo(segment.Count));
            for (int i = 0; i < segment.Count; ++i)
                Assert.That(deserialized.Array[deserialized.Offset + i], Is.EqualTo(segment.Array[segment.Offset + i]));
        }

        [Test]
        public void TestResetSetsPotionAndLength()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteString("I saw");
            writer.WriteLong(0xA_FADED_DEAD_EEL);
            writer.WriteString("and ate it");
            writer.Reset();

            Assert.That(writer.Position, Is.EqualTo(0));

            byte[] data = writer.ToArray();
            Assert.That(data, Is.Empty);
        }

        [Test]
        public void TestReading0LengthBytesAndSize()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteBytesAndSize(new byte[] {});
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Assert.That(reader.ReadBytesAndSize().Length, Is.EqualTo(0));
        }

        [Test]
        public void TestReading0LengthBytes()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteBytes(new byte[] {}, 0, 0);
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
                Assert.Throws<System.IO.EndOfStreamException>(() => read(new NetworkReader(data ?? new byte[] {})));
            }
            // Try reading more than there is data to be read from
            // This should throw EndOfStreamException always
            EnsureThrows(r => r.ReadByte());
            EnsureThrows(r => r.ReadSByte());
            EnsureThrows(r => r.ReadChar());
            EnsureThrows(r => r.ReadBool());
            EnsureThrows(r => r.ReadShort());
            EnsureThrows(r => r.ReadUShort());
            EnsureThrows(r => r.ReadInt());
            EnsureThrows(r => r.ReadUInt());
            EnsureThrows(r => r.ReadLong());
            EnsureThrows(r => r.ReadULong());
            EnsureThrows(r => r.ReadDecimal());
            EnsureThrows(r => r.ReadFloat());
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
        public void TestBool()
        {
            bool[] inputs = { true, false };
            foreach (bool input in inputs)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.WriteBool(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                bool output = reader.ReadBool();
                Assert.That(output, Is.EqualTo(input));
            }
        }

        [Test]
        public void TestBoolNullable()
        {
            bool? input = null;
            NetworkWriter writer = new NetworkWriter();
            writer.WriteBoolNullable(input);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            bool? output = reader.ReadBoolNullable();
            Assert.That(output, Is.EqualTo(input));
        }

        [Test]
        public void TestByte()
        {
            byte[] inputs = { 1, 2, 3, 4 };
            foreach (byte input in inputs)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.WriteByte(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                byte output = reader.ReadByte();
                Assert.That(output, Is.EqualTo(input));
            }
        }

        [Test]
        public void TestByteNullable()
        {
            byte? input = null;
            NetworkWriter writer = new NetworkWriter();
            writer.WriteByteNullable(input);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            byte? output = reader.ReadByteNullable();
            Assert.That(output, Is.EqualTo(input));
        }

        [Test]
        public void TestSByte()
        {
            sbyte[] inputs = { 1, 2, 3, 4 };
            foreach (sbyte input in inputs)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.WriteSByte(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                sbyte output = reader.ReadSByte();
                Assert.That(output, Is.EqualTo(input));
            }
        }

        [Test]
        public void TestSByteNullable()
        {
            sbyte? input = null;
            NetworkWriter writer = new NetworkWriter();
            writer.WriteSByteNullable(input);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            sbyte? output = reader.ReadSByteNullable();
            Assert.That(output, Is.EqualTo(input));
        }

        [Test]
        public void TestVector2()
        {
            Vector2[] inputs = {
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
                writer.WriteVector2(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Vector2 output = reader.ReadVector2();
                Assert.That(output, Is.EqualTo(input));
            }
        }

        [Test]
        public void TestVector2Nullable()
        {
            Vector2? input = null;
            NetworkWriter writer = new NetworkWriter();
            writer.WriteVector2Nullable(input);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Vector2? output = reader.ReadVector2Nullable();
            Assert.That(output, Is.EqualTo(input));
        }

        [Test]
        public void TestVector3()
        {
            Vector3[] inputs = {
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
                writer.WriteVector3(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Vector3 output = reader.ReadVector3();
                Assert.That(output, Is.EqualTo(input));
            }
        }

        [Test]
        public void TestVector3Nullable()
        {
            Vector3? input = null;
            NetworkWriter writer = new NetworkWriter();
            writer.WriteVector3Nullable(input);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Vector3? output = reader.ReadVector3Nullable();
            Assert.That(output, Is.EqualTo(input));
        }

        [Test]
        public void TestVector4()
        {
            Vector4[] inputs = {
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
                writer.WriteVector4(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Vector4 output = reader.ReadVector4();
                Assert.That(output, Is.EqualTo(input));
            }
        }

        [Test]
        public void TestVector4Nullable()
        {
            Vector4? input = null;
            NetworkWriter writer = new NetworkWriter();
            writer.WriteVector4Nullable(input);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Vector4? output = reader.ReadVector4Nullable();
            Assert.That(output, Is.EqualTo(output));
        }

        [Test]
        public void TestVector2Int()
        {
            Vector2Int[] inputs = {
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
                writer.WriteVector2Int(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Vector2Int output = reader.ReadVector2Int();
                Assert.That(output, Is.EqualTo(input));
            }
        }

        [Test]
        public void TestVector2IntNullable()
        {
            Vector2Int? input = null;
            NetworkWriter writer = new NetworkWriter();
            writer.WriteVector2IntNullable(input);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Vector2Int? output = reader.ReadVector2IntNullable();
            Assert.That(output, Is.EqualTo(input));
        }

        [Test]
        public void TestVector3Int()
        {
            Vector3Int[] inputs = {
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
                writer.WriteVector3Int(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Vector3Int output = reader.ReadVector3Int();
                Assert.That(output, Is.EqualTo(input));
            }
        }

        [Test]
        public void TestVector3IntNullable()
        {
            Vector3Int? input = null;
            NetworkWriter writer = new NetworkWriter();
            writer.WriteVector3IntNullable(input);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Vector3Int? output = reader.ReadVector3IntNullable();
            Assert.That(output, Is.EqualTo(input));
        }

        [Test]
        public void TestColor()
        {
            Color[] inputs = {
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
                writer.WriteColor(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Color output = reader.ReadColor();
                Assert.That(output, Is.EqualTo(input));
            }
        }

        [Test]
        public void TestColorNullable()
        {
            Color? input = null;
            NetworkWriter writer = new NetworkWriter();
            writer.WriteColorNullable(input);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Color? output = reader.ReadColorNullable();
            Assert.That(output, Is.EqualTo(input));
        }

        [Test]
        public void TestColor32()
        {
            Color32[] inputs = {
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
                writer.WriteColor32(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Color32 output = reader.ReadColor32();
                Assert.That(output, Is.EqualTo(input));
            }
        }

        [Test]
        public void TestColor32Nullable()
        {
            Color32? input = null;
            NetworkWriter writer = new NetworkWriter();
            writer.WriteColor32Nullable(input);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Color32? output = reader.ReadColor32Nullable();
            Assert.That(output, Is.EqualTo(input));
        }

        [Test]
        public void TestQuaternion()
        {
            Quaternion[] inputs = {
                Quaternion.identity,
                default,
                Quaternion.LookRotation(new Vector3(0.3f,0.4f,0.5f)),
                Quaternion.Euler(45f,56f,Mathf.PI)
            };
            foreach (Quaternion input in inputs)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.WriteQuaternion(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Quaternion output = reader.ReadQuaternion();
                Assert.That(output, Is.EqualTo(input));
            }
        }

        [Test]
        public void TestQuaternionNullable()
        {
            Quaternion? input = null;
            NetworkWriter writer = new NetworkWriter();
            writer.WriteQuaternionNullable(input);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Quaternion? output = reader.ReadQuaternionNullable();
            Assert.That(output, Is.EqualTo(input));
        }

        [Test]
        public void TestRect()
        {
            Rect[] inputs = {
                Rect.zero,
                new Rect(1004.1f,2.001f,4636,400f),
                new Rect(-100.622f,-200f,300f,975.6f),
                new Rect(-100f,435,-30.04f,400f),
                new Rect(55,-200f,-44,-123),
            };
            foreach (Rect input in inputs)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.WriteRect(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Rect output = reader.ReadRect();
                Assert.That(output, Is.EqualTo(input));
            }
        }

        [Test]
        public void TestRectNullable()
        {
            Rect? input = null;
            NetworkWriter writer = new NetworkWriter();
            writer.WriteRectNullable(input);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Rect? output = reader.ReadRectNullable();
            Assert.That(output, Is.EqualTo(input));
        }

        [Test]
        public void TestPlane()
        {
            Plane[] inputs = {
                new Plane(new Vector3(-0.24f,0.34f,0.2f), 120.2f),
                new Plane(new Vector3(0.133f,0.34f,0.122f), -10.135f),
                new Plane(new Vector3(0.133f,-0.0f,float.MaxValue), -13.3f),
                new Plane(new Vector3(0.1f,-0.2f,0.3f), 14.5f),
            };
            foreach (Plane input in inputs)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.WritePlane(input);
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
        public void TestPlaneNullable()
        {
            Plane? input = null;
            NetworkWriter writer = new NetworkWriter();
            writer.WritePlaneNullable(input);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Plane? output = reader.ReadPlaneNullable();
            Assert.That(output, Is.EqualTo(input));
        }

        [Test]
        public void TestRay()
        {
            Ray[] inputs = {
                new Ray(Vector3.up,Vector3.down),
                new Ray(new Vector3(0.1f,0.2f,0.3f), new Vector3(0.4f,0.5f,0.6f)),
                new Ray(new Vector3(-0.3f,0.5f,0.999f), new Vector3(1f,100.1f,20f)),
            };
            foreach (Ray input in inputs)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.WriteRay(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Ray output = reader.ReadRay();
                Assert.That((output.direction - input.direction).magnitude, Is.LessThan(1e-6f));
                Assert.That(output.origin, Is.EqualTo(input.origin));
            }
        }

        [Test]
        public void TestRayNullable()
        {
            Ray? input = null;
            NetworkWriter writer = new NetworkWriter();
            writer.WriteRayNullable(input);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Ray? output = reader.ReadRayNullable();
            Assert.That(output, Is.EqualTo(input));
        }

        [Test]
        public void TestLayerMask()
        {
            LayerMask originalLayerMask = new LayerMask();
            originalLayerMask.value = 42;
            NetworkWriter writer = new NetworkWriter();
            writer.WriteLayerMask(originalLayerMask);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            LayerMask readLayerMask = reader.ReadLayerMask();
            Assert.That(readLayerMask, Is.EqualTo(originalLayerMask));
        }

        [Test]
        public void TestLayerMaskNullable()
        {
            LayerMask? input = null;
            NetworkWriter writer = new NetworkWriter();
            writer.WriteLayerMaskNullable(input);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            LayerMask? output = reader.ReadLayerMaskNullable();
            Assert.That(output, Is.EqualTo(input));
        }

        [Test]
        public void TestMatrix4x4()
        {
            Matrix4x4[] inputs = {
                Matrix4x4.identity,
                Matrix4x4.zero,
                Matrix4x4.Scale(Vector3.one * 0.12345f),
                Matrix4x4.LookAt(Vector2.up,Vector3.right,Vector3.forward),
                Matrix4x4.Rotate(Quaternion.LookRotation(Vector3.one)),
            };
            foreach (Matrix4x4 input in inputs)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.WriteMatrix4x4(input);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                Matrix4x4 output = reader.ReadMatrix4x4();
                Assert.That(output, Is.EqualTo(input));
            }
        }

        [Test]
        public void TestMatrix4x4Nullable()
        {
            Matrix4x4? input = null;
            NetworkWriter writer = new NetworkWriter();
            writer.WriteMatrix4x4Nullable(input);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Matrix4x4? output = reader.ReadMatrix4x4Nullable();
            Assert.That(output, Is.EqualTo(input));
        }

        [Test]
        public void TestReadingInvalidString()
        {
            // These are all bytes which never show up in valid UTF8 encodings.
            // NetworkReader should gracefully handle maliciously crafted input.
            byte[] invalidUTF8bytes = {
                0xC0, 0xC1, 0xF5, 0xF6,
                0xF7, 0xF8, 0xF9, 0xFA,
                0xFB, 0xFC, 0xFD, 0xFE,
                0xFF,
            };
            foreach (byte invalid in invalidUTF8bytes)
            {
                NetworkWriter writer = new NetworkWriter();
                writer.WriteString("an uncorrupted string");
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
            writer.WriteString("a string longer than 10 bytes");
            writer.Reset();
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Assert.Throws<System.IO.EndOfStreamException>(() => reader.ReadString());
        }

        [Test]
        public void TestToArray()
        {
            // write 2 bytes
            NetworkWriter writer = new NetworkWriter();
            writer.WriteByte(1);
            writer.WriteByte(2);

            // .ToArray() length is 2?
            Assert.That(writer.ToArray().Length, Is.EqualTo(2));

            // set position back by one
            writer.Position = 1;

            // Changing the position alter the size of the data
            Assert.That(writer.ToArray().Length, Is.EqualTo(1));
        }

        [Test]
        public void TestToArraySegment()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteString("hello");
            writer.WriteString("world");

            NetworkReader reader = new NetworkReader(writer.ToArraySegment());
            Assert.That(reader.ReadString(), Is.EqualTo("hello"));
            Assert.That(reader.ReadString(), Is.EqualTo("world"));
        }

        // sometimes we may serialize nothing, then call ToArraySegment.
        // make sure this works even if empty.
        [Test]
        public void TestToArraySegment_EmptyContent()
        {
            NetworkWriter writer = new NetworkWriter();
            ArraySegment<byte> segment = writer.ToArraySegment();
            Assert.That(segment.Count, Is.EqualTo(0));
        }

        [Test]
        public void TestChar()
        {
            char a = 'a';
            char u = 'ⓤ';

            NetworkWriter writer = new NetworkWriter();
            writer.WriteChar(a);
            writer.WriteChar(u);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            char a2 = reader.ReadChar();
            Assert.That(a2, Is.EqualTo(a));
            char u2 = reader.ReadChar();
            Assert.That(u2, Is.EqualTo(u));
        }

        [Test]
        public void TestCharNullable()
        {
            char? input = null;
            NetworkWriter writer = new NetworkWriter();
            writer.WriteCharNullable(input);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            char? output = reader.ReadCharNullable();
            Assert.That(output, Is.EqualTo(input));
        }

        [Test]
        public void TestUnicodeString()
        {
            string[] weirdUnicode = {
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
                writer.WriteString(weird);
                byte[] data = writer.ToArray();
                NetworkReader reader = new NetworkReader(data);
                string str = reader.ReadString();
                Assert.That(str, Is.EqualTo(weird));
            }
        }

        [Test]
        public void TestGuid()
        {
            Guid originalGuid = new Guid("0123456789abcdef9876543210fedcba");
            NetworkWriter writer = new NetworkWriter();
            writer.WriteGuid(originalGuid);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            Guid readGuid = reader.ReadGuid();
            Assert.That(readGuid, Is.EqualTo(originalGuid));
        }

        [Test]
        public void TestGuidNullable()
        {
            Guid? input = null;
            NetworkWriter writer = new NetworkWriter();
            writer.WriteGuidNullable(input);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Guid? output = reader.ReadGuidNullable();
            Assert.That(output, Is.EqualTo(input));
        }

        [Test]
        public void TestFloats()
        {
            float[] weirdFloats = {
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
                writer.WriteFloat(weird);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                float readFloat = reader.ReadFloat();
                Assert.That(readFloat, Is.EqualTo(weird));
            }
        }

        [Test]
        public void TestFloatNullable()
        {
            float? input = null;
            NetworkWriter writer = new NetworkWriter();
            writer.WriteFloatNullable(input);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            float? output = reader.ReadFloatNullable();
            Assert.That(output, Is.EqualTo(input));
        }

        [Test]
        public void TestDoubles()
        {
            double[] weirdDoubles = {
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
                writer.WriteDouble(weird);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                double readDouble = reader.ReadDouble();
                Assert.That(readDouble, Is.EqualTo(weird));
            }
        }

        [Test]
        public void TestDoubleNullable()
        {
            double? input = null;
            NetworkWriter writer = new NetworkWriter();
            writer.WriteDoubleNullable(input);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            double? output = reader.ReadDoubleNullable();
            Assert.That(output, Is.EqualTo(input));
        }

        [Test]
        public void TestDecimals()
        {
            decimal[] weirdDecimals = {
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
                writer.WriteDecimal(weird);
                NetworkReader reader = new NetworkReader(writer.ToArray());
                decimal readDecimal = reader.ReadDecimal();
                Assert.That(readDecimal, Is.EqualTo(weird));
            }
        }

        [Test]
        public void TestDecimalNullable()
        {
            decimal? input = null;
            NetworkWriter writer = new NetworkWriter();
            writer.WriteDecimalNullable(input);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            decimal? output = reader.ReadDecimalNullable();
            Assert.That(output, Is.EqualTo(input));
        }

        [Test]
        public void TestFloatBinaryCompatibility()
        {
            float[] weirdFloats = {
                ((float) Math.PI) / 3.0f,
                ((float) Math.E) / 3.0f
            };
            byte[] expected = {
                146, 10,134, 63,
                197,245,103, 63,
            };
            NetworkWriter writer = new NetworkWriter();
            foreach (float weird in weirdFloats)
            {
                writer.WriteFloat(weird);
            }
            Assert.That(writer.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void TestDoubleBinaryCompatibility()
        {
            double[] weirdDoubles = {
                Math.PI / 3.0d,
                Math.E / 3.0d
            };
            byte[] expected = {
                101,115, 45, 56, 82,193,240, 63,
                140,116,112,185,184,254,236, 63,
            };
            NetworkWriter writer = new NetworkWriter();
            foreach (double weird in weirdDoubles)
            {
                writer.WriteDouble(weird);
            }
            Assert.That(writer.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void TestDecimalBinaryCompatibility()
        {
            decimal[] weirdDecimals = {
                ((decimal) Math.PI) / 3.0m,
                ((decimal) Math.E) / 3.0m
            };
            byte[] expected = {
                0x00, 0x00, 0x1C, 0x00, 0x12, 0x37, 0xD6, 0x21, 0xAB, 0xEA,
                0x84, 0x0A, 0x5B, 0x5E, 0xB1, 0x03, 0x00, 0x00, 0x0E, 0x00,
                0x00, 0x00, 0x00, 0x00, 0xF0, 0x6D, 0xC2, 0xA4, 0x68, 0x52,
                0x00, 0x00
            };
            NetworkWriter writer = new NetworkWriter();
            foreach (decimal weird in weirdDecimals)
            {
                writer.WriteDecimal(weird);
            }
            //Debug.Log(BitConverter.ToString(writer.ToArray()));
            Assert.That(writer.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void TestByteEndianness()
        {
            byte[] values = { 0x12, 0x43, 0x00, 0xff, 0xab, 0x02, 0x20 };
            byte[] expected = { 0x12, 0x43, 0x00, 0xff, 0xab, 0x02, 0x20 };
            NetworkWriter writer = new NetworkWriter();
            foreach (byte value in values)
            {
                writer.WriteByte(value);
            }
            Assert.That(writer.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void TestUShortEndianness()
        {
            ushort[] values = { 0x0000, 0x1234, 0xabcd, 0xF00F, 0x0FF0, 0xbeef };
            byte[] expected = { 0x00, 0x00, 0x34, 0x12, 0xcd, 0xab, 0x0F, 0xF0, 0xF0, 0x0F, 0xef, 0xbe };
            NetworkWriter writer = new NetworkWriter();
            foreach (ushort value in values)
            {
                writer.WriteUShort(value);
            }
            Assert.That(writer.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void TestUIntEndianness()
        {
            uint[] values = { 0x12345678, 0xabcdef09, 0xdeadbeef };
            byte[] expected = { 0x78, 0x56, 0x34, 0x12, 0x09, 0xef, 0xcd, 0xab, 0xef, 0xbe, 0xad, 0xde };
            NetworkWriter writer = new NetworkWriter();
            foreach (uint value in values)
            {
                writer.WriteUInt(value);
            }
            Assert.That(writer.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void TestULongEndianness()
        {
            ulong[] values = { 0x0123456789abcdef, 0xdeaded_beef_c0ffee };
            byte[] expected = { 0xef, 0xcd, 0xab, 0x89, 0x67, 0x45, 0x23, 0x01, 0xee, 0xff, 0xc0, 0xef, 0xbe, 0xed, 0xad, 0xde };
            NetworkWriter writer = new NetworkWriter();
            foreach (ulong value in values)
            {
                writer.WriteULong(value);
            }
            Assert.That(writer.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void TestSbyteEndianness()
        {
            byte[] values = { 0x12, 0x43, 0x00, 0xff, 0xab, 0x02, 0x20 };
            byte[] expected = { 0x12, 0x43, 0x00, 0xff, 0xab, 0x02, 0x20 };
            NetworkWriter writer = new NetworkWriter();
            foreach (byte value in values)
            {
                writer.WriteSByte((sbyte)value);
            }
            Assert.That(writer.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void TestShortEndianness()
        {
            ushort[] values = { 0x0000, 0x1234, 0xabcd, 0xF00F, 0x0FF0, 0xbeef };
            byte[] expected = { 0x00, 0x00, 0x34, 0x12, 0xcd, 0xab, 0x0F, 0xF0, 0xF0, 0x0F, 0xef, 0xbe };
            NetworkWriter writer = new NetworkWriter();
            foreach (ushort value in values)
            {
                writer.WriteShort((short)value);
            }
            Assert.That(writer.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void TestIntEndianness()
        {
            uint[] values = { 0x12345678, 0xabcdef09, 0xdeadbeef };
            byte[] expected = { 0x78, 0x56, 0x34, 0x12, 0x09, 0xef, 0xcd, 0xab, 0xef, 0xbe, 0xad, 0xde };
            NetworkWriter writer = new NetworkWriter();
            foreach (uint value in values)
            {
                writer.WriteInt((int)value);
            }
            Assert.That(writer.ToArray(), Is.EqualTo(expected));
        }

        [Test]
        public void TestLongEndianness()
        {
            ulong[] values = { 0x0123456789abcdef, 0xdeaded_beef_c0ffee };
            byte[] expected = { 0xef, 0xcd, 0xab, 0x89, 0x67, 0x45, 0x23, 0x01, 0xee, 0xff, 0xc0, 0xef, 0xbe, 0xed, 0xad, 0xde };
            NetworkWriter writer = new NetworkWriter();
            foreach (ulong value in values)
            {
                writer.WriteLong((long)value);
            }
            Assert.That(writer.ToArray(), Is.EqualTo(expected));
        }

        // test to avoid https://github.com/vis2k/Mirror/issues/3258
        [Test]
        public void WriteString_EnsuresCapacity()
        {
            NetworkWriter writer = new NetworkWriter();

            // jump to near the end
            int initialPosition = writer.Position;
            writer.Position = writer.Capacity - 4;

            // try to write a string
            writer.WriteString("a test string");

            // buffer should have resized without throwing any exceptions
            Assert.That(writer.Position, Is.GreaterThan(initialPosition));
        }


        [Test]
        public void TestWritingAndReading()
        {
            // write all simple types once
            NetworkWriter writer = new NetworkWriter();
            writer.WriteChar((char)1);
            writer.WriteByte(2);
            writer.WriteSByte(3);
            writer.WriteBool(true);
            writer.WriteShort(4);
            writer.WriteUShort(5);
            writer.WriteInt(6);
            writer.WriteUInt(7U);
            writer.WriteLong(8L);
            writer.WriteULong(9UL);
            writer.WriteFloat(10.0F);
            writer.WriteDouble(11.0D);
            writer.WriteDecimal(12);
            writer.WriteString(null);
            writer.WriteString("");
            writer.WriteString("13");
            // just the byte array, no size info etc.
            writer.WriteBytes(new byte[] { 14, 15 }, 0, 2);
            // [SyncVar] struct values can have uninitialized byte arrays, null needs to be supported
            writer.WriteBytesAndSize(null);
            // buffer, no-offset, count
            writer.WriteBytesAndSize(new byte[] { 17, 18 }, 0, 2);
            // buffer, offset, count
            writer.WriteBytesAndSize(new byte[] { 19, 20, 21 }, 1, 2);
            // size, buffer
            writer.WriteBytesAndSize(new byte[] { 22, 23 }, 0, 2);

            // read them
            NetworkReader reader = new NetworkReader(writer.ToArray());

            Assert.That(reader.ReadChar(), Is.EqualTo(1));
            Assert.That(reader.ReadByte(), Is.EqualTo(2));
            Assert.That(reader.ReadSByte(), Is.EqualTo(3));
            Assert.That(reader.ReadBool(), Is.True);
            Assert.That(reader.ReadShort(), Is.EqualTo(4));
            Assert.That(reader.ReadUShort(), Is.EqualTo(5));
            Assert.That(reader.ReadInt(), Is.EqualTo(6));
            Assert.That(reader.ReadUInt(), Is.EqualTo(7));
            Assert.That(reader.ReadLong(), Is.EqualTo(8));
            Assert.That(reader.ReadULong(), Is.EqualTo(9));
            Assert.That(reader.ReadFloat(), Is.EqualTo(10));
            Assert.That(reader.ReadDouble(), Is.EqualTo(11));
            Assert.That(reader.ReadDecimal(), Is.EqualTo(12));
            // writing null string should write null in Mirror ("" in original HLAPI)
            Assert.That(reader.ReadString(), Is.Null);
            Assert.That(reader.ReadString(), Is.EqualTo(""));
            Assert.That(reader.ReadString(), Is.EqualTo("13"));

            Assert.That(reader.ReadBytes(2), Is.EqualTo(new byte[] { 14, 15 }));

            Assert.That(reader.ReadBytesAndSize(), Is.Null);

            Assert.That(reader.ReadBytesAndSize(), Is.EqualTo(new byte[] { 17, 18 }));

            Assert.That(reader.ReadBytesAndSize(), Is.EqualTo(new byte[] { 20, 21 }));

            Assert.That(reader.ReadBytesAndSize(), Is.EqualTo(new byte[] { 22, 23 }));
        }

        [Test]
        public void TestWritingUri()
        {

            Uri testUri = new Uri("https://www.mirror-networking.com?somthing=other");

            NetworkWriter writer = new NetworkWriter();
            writer.WriteUri(testUri);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            Assert.That(reader.ReadUri(), Is.EqualTo(testUri));
        }

        // URI null support test for https://github.com/vis2k/Mirror/pull/2796/
        [Test]
        public void TestWritingNullUri()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteUri(null);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            Assert.That(reader.ReadUri(), Is.EqualTo(null));
        }

        [Test]
        public void TestList()
        {
            List<int> original = new List<int>() { 1, 2, 3, 4, 5 };
            NetworkWriter writer = new NetworkWriter();
            writer.Write(original);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            List<int> readList = reader.Read<List<int>>();
            Assert.That(readList, Is.EqualTo(original));
        }

        [Test]
        public void TestNullList()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.Write<List<int>>(null);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            List<int> readList = reader.Read<List<int>>();
            Assert.That(readList, Is.Null);
        }

        // writer.Write<T> for HashSet only works if it's actually used by weaver somewhere.
        // so for TestHashSet() to pass, we need to pretend using a HashSet<int> somewhere.
        class HashSetNetworkBehaviour : NetworkBehaviour
        {
            [Command]
            public void CmdHashSet(HashSet<int> hashSet) {}
        }

        [Test] // requires HashSetNetworkBehaviour to exits!
        public void TestHashSet()
        {
            HashSet<int> original = new HashSet<int>() { 1, 2, 3, 4, 5 };
            NetworkWriter writer = new NetworkWriter();
            writer.Write(original);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            HashSet<int> readHashSet = reader.Read<HashSet<int>>();
            Assert.That(readHashSet, Is.EqualTo(original));
        }

        [Test] // requires HashSetNetworkBehaviour to exits!
        public void TestNullHashSet()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.Write<HashSet<int>>(null);

            NetworkReader reader = new NetworkReader(writer.ToArray());
            HashSet<int> readHashSet = reader.Read<HashSet<int>>();
            Assert.That(readHashSet, Is.Null);
        }


        const int testArraySize = 4;
        [Test]
        [Description("ReadArray should throw if it is trying to read more than length of segment, this is to stop allocation attacks")]
        public void TestArrayDoesNotThrowWithCorrectLength()
        {
            NetworkWriter writer = new NetworkWriter();
            WriteGoodArray();

            NetworkReader reader = new NetworkReader(writer.ToArray());
            Assert.DoesNotThrow(() =>
            {
                _ = reader.ReadArray<int>();
            });

            void WriteGoodArray()
            {
                writer.WriteInt(testArraySize);
                int[] array = new int[testArraySize] { 1, 2, 3, 4 };
                for (int i = 0; i < array.Length; i++)
                    writer.Write(array[i]);
            }
        }
        [Test]
        [Description("ReadArray should throw if it is trying to read more than length of segment, this is to stop allocation attacks")]
        [TestCase(testArraySize * sizeof(int), Description = "max allowed value to allocate array")]
        [TestCase(testArraySize * 2)]
        [TestCase(testArraySize + 1, Description = "min allowed to allocate")]
        public void TestArrayThrowsIfLengthIsWrong(int badLength)
        {
            NetworkWriter writer = new NetworkWriter();
            WriteBadArray();

            NetworkReader reader = new NetworkReader(writer.ToArray());
            Assert.Throws<EndOfStreamException>(() => {
                _ = reader.ReadArray<int>();
            });

            void WriteBadArray()
            {
                // Reader/Writer encode null as count=0 and [] as count=1 (+1 offset)
                writer.WriteVarUInt((uint)(badLength+1)); // Reader/Writer encode size headers as VarInt
                int[] array = new int[testArraySize] { 1, 2, 3, 4 };
                for (int i = 0; i < array.Length; i++)
                    writer.Write(array[i]);
            }
        }

        [Test]
        [TestCase(20_000)]
        [TestCase(int.MaxValue)]
        [TestCase(int.MaxValue - 1)]
        public void TestReadBytes_LengthIsTooBig(int badLength)
        {
            // write bad array
            NetworkWriter writer = new NetworkWriter();
            writer.WriteInt(badLength);
            int[] array = new int[testArraySize] { 1, 2, 3, 4 };
            for (int i = 0; i < array.Length; i++)
                writer.Write(array[i]);

            // attempt to read it
            NetworkReader reader = new NetworkReader(writer.ToArray());
            EndOfStreamException exception = Assert.Throws<EndOfStreamException>(() =>
            {
                _ = reader.ReadBytes(badLength);
            });
        }

        [Test]
        [TestCase(testArraySize * sizeof(int) + 1, Description = "min read count is 1 byte, 16 array bytes are writen so 17 should throw error")]
        [TestCase(20_000)]
        [TestCase(int.MaxValue)]
        [TestCase(int.MaxValue - 1)]
        public void TestReadList_LengthIsTooBig(int badLength)
        {
            // write bad array
            NetworkWriter writer = new NetworkWriter();
            writer.WriteInt(badLength);
            int[] array = new int[testArraySize] { 1, 2, 3, 4 };
            for (int i = 0; i < array.Length; i++)
                writer.Write(array[i]);

            // attempt to read it
            NetworkReader reader = new NetworkReader(writer.ToArray());
            EndOfStreamException exception = Assert.Throws<EndOfStreamException>(() =>
            {
                _ = reader.ReadList<int>();
            });
        }

        [Test]
        [TestCase(testArraySize * sizeof(int) + 1, Description = "min read count is 1 byte, 16 array bytes are writen so 17 should throw error")]
        [TestCase(20_000)]
        [TestCase(int.MaxValue)]
        [TestCase(int.MaxValue - 1)]
        public void TestReadArray_LengthIsTooBig(int badLength)
        {
            // write bad array
            NetworkWriter writer = new NetworkWriter();
            writer.WriteInt(badLength);
            int[] array = new int[testArraySize] { 1, 2, 3, 4 };
            for (int i = 0; i < array.Length; i++)
                writer.Write(array[i]);

            // attempt to read it
            NetworkReader reader = new NetworkReader(writer.ToArray());
            EndOfStreamException exception = Assert.Throws<EndOfStreamException>(() =>
            {
                _ = reader.ReadArray<int>();
            });
        }

        [Test]
        public void TestNetworkBehaviour()
        {
            // create spawned because we will look up netId in .spawned
            CreateNetworkedAndSpawn(out _, out _, out RpcNetworkIdentityBehaviour behaviour,
                                    out _, out _, out _);

            NetworkWriter writer = new NetworkWriter();
            writer.WriteNetworkBehaviour(behaviour);

            byte[] bytes = writer.ToArray();

            Assert.That(bytes.Length, Is.EqualTo(5), "Networkbehaviour should be 5 bytes long.");

            NetworkReader reader = new NetworkReader(bytes);
            RpcNetworkIdentityBehaviour actual = reader.ReadNetworkBehaviour<RpcNetworkIdentityBehaviour>();
            Assert.That(actual, Is.EqualTo(behaviour), "Read should find the same behaviour as written");
        }

        [Test]
        public void TestNetworkBehaviourNull()
        {
            NetworkWriter writer = new NetworkWriter();
            writer.WriteNetworkBehaviour(null);

            byte[] bytes = writer.ToArray();

            Assert.That(bytes.Length, Is.EqualTo(4), "null Networkbehaviour should be 4 bytes long.");

            NetworkReader reader = new NetworkReader(bytes);
            RpcNetworkIdentityBehaviour actual = reader.ReadNetworkBehaviour<RpcNetworkIdentityBehaviour>();
            Assert.That(actual, Is.Null, "should read null");

            Assert.That(reader.Position, Is.EqualTo(4), "should read 4 bytes when netid is 0");
        }

        // test for https://github.com/MirrorNetworking/Mirror/issues/3399
        [Test]
        public void TestNetworkBehaviourNotSpawned()
        {
            CreateNetworked(out _, out _, out RpcNetworkIdentityBehaviour component);
            NetworkWriter writer = new NetworkWriter();
            writer.WriteNetworkBehaviour(component);

            byte[] bytes = writer.ToArray();

            Assert.That(bytes.Length, Is.EqualTo(4), "unspawned Networkbehaviour should be 4 bytes long.");

            NetworkReader reader = new NetworkReader(bytes);
            RpcNetworkIdentityBehaviour actual = reader.ReadNetworkBehaviour<RpcNetworkIdentityBehaviour>();
            Assert.That(actual, Is.Null, "should read null");

            Assert.That(reader.Position, Is.EqualTo(4), "should read 4 bytes when netid is 0");
        }

        // test to prevent https://github.com/vis2k/Mirror/issues/2972
        [Test]
        public void TestNetworkBehaviourDoesntExistOnClient()
        {
            // create spawned because we will look up netId in .spawned
            CreateNetworkedAndSpawn(out _, out _, out RpcNetworkIdentityBehaviour serverComponent,
                                    out _, out _, out RpcNetworkIdentityBehaviour clientComponent);

            // write on server where it's != null and exists
            NetworkWriter writer = new NetworkWriter();
            writer.WriteNetworkBehaviour(serverComponent);

            byte[] bytes = writer.ToArray();
            Assert.That(bytes.Length, Is.EqualTo(5), "Networkbehaviour should be 5 bytes long.");

            // make it disappear / despawn on client
            NetworkServer.spawned.Remove(serverComponent.netId);
            NetworkClient.spawned.Remove(clientComponent.netId);

            // reading should return null component
            NetworkReader reader = new NetworkReader(bytes);
            RpcNetworkIdentityBehaviour actual = reader.ReadNetworkBehaviour<RpcNetworkIdentityBehaviour>();
            Assert.That(actual, Is.Null);

            // IMPORTANT: should have read EXACTLY as much as was written.
            // even if NetworkBehaviour wasn't found on client.
            // otherwise data gets corrupted.
            Assert.That(reader.Position, Is.EqualTo(writer.Position));
        }

        [Test]
        [Description("Uses Generic read function to check weaver correctly creates it")]
        public void TestNetworkBehaviourWeaverGenerated()
        {
            // create spawned because we will look up netId in .spawned
            CreateNetworkedAndSpawn(out _, out _, out RpcNetworkIdentityBehaviour behaviour,
                                    out _, out _, out _);

            NetworkWriter writer = new NetworkWriter();
            writer.Write(behaviour);

            byte[] bytes = writer.ToArray();

            Assert.That(bytes.Length, Is.EqualTo(5), "Networkbehaviour should be 5 bytes long.");

            NetworkReader reader = new NetworkReader(bytes);
            RpcNetworkIdentityBehaviour actual = reader.Read<RpcNetworkIdentityBehaviour>();
            Assert.That(actual, Is.EqualTo(behaviour), "Read should find the same behaviour as written");
        }

        // test to make sure unspawned / prefab GameObjects can't be synced.
        // they would be null on the other end, and it might not be obvious why.
        // https://github.com/vis2k/Mirror/issues/2060
        [Test]
        public void TestWritingUnspawnedGameObject()
        {
            // create GO + NI, but unspawned
            CreateNetworked(out GameObject go, out _);

            // serializing in rpc/cmd/message should warn if unspawned.
            LogAssert.Expect(LogType.Warning, new Regex("Attempted to serialize unspawned.*"));
            NetworkWriter writer = new NetworkWriter();
            writer.WriteGameObject(go);
        }

        // test to make sure unspawned / prefab GameObjects can't be synced.
        // they would be null on the other end, and it might not be obvious why.
        // https://github.com/vis2k/Mirror/issues/2060
        [Test]
        public void TestWritingUnspawnedNetworkIdentity()
        {
            // create GO + NI, but unspawned
            CreateNetworked(out _, out NetworkIdentity identity);

            // serializing in rpc/cmd/message should warn if unspawned.
            LogAssert.Expect(LogType.Warning, new Regex("Attempted to serialize unspawned.*"));
            NetworkWriter writer = new NetworkWriter();
            writer.WriteNetworkIdentity(identity);
        }

        [Test]
        public void WriteTexture2D_black()
        {
            // write
            NetworkWriter writer = new NetworkWriter();
            writer.WriteTexture2D(Texture2D.blackTexture);

            // read
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Texture2D texture = reader.ReadTexture2D();

            // compare
            Assert.That(texture.width, Is.EqualTo(Texture2D.blackTexture.width));
            Assert.That(texture.height, Is.EqualTo(Texture2D.blackTexture.height));
            Assert.That(texture.GetPixels32().SequenceEqual(Texture2D.blackTexture.GetPixels32()));
        }

        [Test]
        public void WriteTexture2D_normal()
        {
            // write
            NetworkWriter writer = new NetworkWriter();
            writer.WriteTexture2D(Texture2D.normalTexture);

            // read
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Texture2D texture = reader.ReadTexture2D();

            // compare
            Assert.That(texture.width, Is.EqualTo(Texture2D.normalTexture.width));
            Assert.That(texture.height, Is.EqualTo(Texture2D.normalTexture.height));
            Assert.That(texture.GetPixels32().SequenceEqual(Texture2D.normalTexture.GetPixels32()));
        }

        // test to prevent https://github.com/vis2k/Mirror/issues/3144
        [Test]
        public void WriteTexture2D_Null()
        {
            // write
            NetworkWriter writer = new NetworkWriter();
            writer.WriteTexture2D(null);

            // read
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Texture2D texture = reader.ReadTexture2D();
            Assert.That(texture, Is.Null);
        }

        [Test]
        public void WriteSprite_normal()
        {
            // create a test sprite
            Sprite example = Sprite.Create(Texture2D.normalTexture, new Rect(1, 1, 2, 2), Vector2.zero);

            // write
            NetworkWriter writer = new NetworkWriter();
            writer.WriteSprite(example);

            // read
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Sprite sprite = reader.ReadSprite();

            // compare
            Assert.That(sprite.rect, Is.EqualTo(example.rect));
            Assert.That(sprite.pivot, Is.EqualTo(example.pivot));
            Assert.That(sprite.texture.width, Is.EqualTo(example.texture.width));
            Assert.That(sprite.texture.height, Is.EqualTo(example.texture.height));
            Assert.That(sprite.texture.GetPixels32().SequenceEqual(example.texture.GetPixels32()));
        }

        // test to prevent https://github.com/vis2k/Mirror/issues/3144
        [Test]
        public void WriteSprite_Null()
        {
            // write
            NetworkWriter writer = new NetworkWriter();
            writer.WriteSprite(null);

            // read
            NetworkReader reader = new NetworkReader(writer.ToArray());
            Sprite sprite = reader.ReadSprite();
            Assert.That(sprite, Is.Null);
        }

        // test for https://github.com/MirrorNetworking/Mirror/pull/3492/
        // we have the same test once in Editor tests and once in Runtime tests.
        // editor always passes, even before the fix.
        // runtime gets the FieldAccessException.
        public class ClassWithProtected
        {
            // should serialize
            public int field1;

            // should NOT serialize
            protected int field2;
            private int field3;
        }
        public struct MyMessage : NetworkMessage
        {
            public ClassWithProtected field;
        }
        [Test]
        public void WriteProtected()
        {
            NetworkWriter writer = new NetworkWriter();
            MyMessage message = new MyMessage();
            message.field = new ClassWithProtected { field1 = 42 };
            writer.Write(message);

            NetworkReader reader = new NetworkReader(writer);
            MyMessage read = reader.Read<MyMessage>();
            Assert.That(read.field.field1, Is.EqualTo(42));
        }
    }
}
