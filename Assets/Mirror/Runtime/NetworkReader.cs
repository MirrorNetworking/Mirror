using System;
using System.IO;
using UnityEngine;

namespace Mirror
{
    public class NetworkReader
    {
        readonly BinaryReader reader;

        public NetworkReader(byte[] buffer)
        {
            reader = new BinaryReader(new MemoryStream(buffer));
        }

        // 'int' is the best type for .Position. 'short' is too small if we send >32kb which would result in negative .Position
        // -> converting long to int is fine until 2GB of data (MAX_INT), so we don't have to worry about overflows here
        public int Position { get { return (int)reader.BaseStream.Position; }  set { reader.BaseStream.Position = value; } }
        public int Length { get { return (int)reader.BaseStream.Length; } }

        public byte ReadByte() { return reader.ReadByte(); }
        public sbyte ReadSByte() { return reader.ReadSByte(); }
        public char ReadChar() { return reader.ReadChar(); }
        public bool ReadBoolean() { return reader.ReadBoolean(); }
        public short ReadInt16() { return reader.ReadInt16(); }
        public ushort ReadUInt16() { return reader.ReadUInt16(); }
        public int ReadInt32() { return reader.ReadInt32(); }
        public uint ReadUInt32() { return reader.ReadUInt32(); }
        public long ReadInt64() { return reader.ReadInt64(); }
        public ulong ReadUInt64() { return reader.ReadUInt64(); }
        public decimal ReadDecimal() { return reader.ReadDecimal(); }
        public float ReadSingle() { return reader.ReadSingle(); }
        public double ReadDouble() { return reader.ReadDouble(); }

        public string ReadString()
        {
            return reader.ReadBoolean() ? reader.ReadString() : null; // null support, see NetworkWriter
        }

        public byte[] ReadBytes(int count)
        {
            return reader.ReadBytes(count);
        }

        public byte[] ReadBytesAndSize()
        {
            // notNull? (see NetworkWriter)
            bool notNull = reader.ReadBoolean();
            if (notNull)
            {
                uint size = ReadPackedUInt32();
                return reader.ReadBytes((int)size);
            }
            return null;
        }

        // http://sqlite.org/src4/doc/trunk/www/varint.wiki
        // NOTE: big endian.
        public UInt32 ReadPackedUInt32()
        {
            UInt64 value = ReadPackedUInt64();
            if (value > UInt32.MaxValue)
            {
                throw new IndexOutOfRangeException("ReadPackedUInt32() failure, value too large");
            }
            return (UInt32)value;
        }

        public UInt64 ReadPackedUInt64()
        {
            byte a0 = ReadByte();
            if (a0 < 241)
            {
                return a0;
            }

            byte a1 = ReadByte();
            if (a0 >= 241 && a0 <= 248)
            {
                return 240 + 256 * (a0 - ((UInt64)241)) + a1;
            }

            byte a2 = ReadByte();
            if (a0 == 249)
            {
                return 2288 + (((UInt64)256) * a1) + a2;
            }

            byte a3 = ReadByte();
            if (a0 == 250)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16);
            }

            byte a4 = ReadByte();
            if (a0 == 251)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24);
            }

            byte a5 = ReadByte();
            if (a0 == 252)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24) + (((UInt64)a5) << 32);
            }

            byte a6 = ReadByte();
            if (a0 == 253)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24) + (((UInt64)a5) << 32) + (((UInt64)a6) << 40);
            }

            byte a7 = ReadByte();
            if (a0 == 254)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24) + (((UInt64)a5) << 32) + (((UInt64)a6) << 40) + (((UInt64)a7) << 48);
            }

            byte a8 = ReadByte();
            if (a0 == 255)
            {
                return a1 + (((UInt64)a2) << 8) + (((UInt64)a3) << 16) + (((UInt64)a4) << 24) + (((UInt64)a5) << 32) + (((UInt64)a6) << 40) + (((UInt64)a7) << 48)  + (((UInt64)a8) << 56);
            }

            throw new IndexOutOfRangeException("ReadPackedUInt64() failure: " + a0);
        }

        public Vector2 ReadVector2()
        {
            return new Vector2(ReadSingle(), ReadSingle());
        }

        public Vector3 ReadVector3()
        {
            return new Vector3(ReadSingle(), ReadSingle(), ReadSingle());
        }

        public Vector4 ReadVector4()
        {
            return new Vector4(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }

        public Color ReadColor()
        {
            return new Color(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }

        public Color32 ReadColor32()
        {
            return new Color32(ReadByte(), ReadByte(), ReadByte(), ReadByte());
        }

        public Quaternion ReadQuaternion()
        {
            return new Quaternion(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }

        public Rect ReadRect()
        {
            return new Rect(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }

        public Plane ReadPlane()
        {
            return new Plane(ReadVector3(), ReadSingle());
        }

        public Ray ReadRay()
        {
            return new Ray(ReadVector3(), ReadVector3());
        }

        public Matrix4x4 ReadMatrix4x4()
        {
            Matrix4x4 m = new Matrix4x4();
            m.m00 = ReadSingle();
            m.m01 = ReadSingle();
            m.m02 = ReadSingle();
            m.m03 = ReadSingle();
            m.m10 = ReadSingle();
            m.m11 = ReadSingle();
            m.m12 = ReadSingle();
            m.m13 = ReadSingle();
            m.m20 = ReadSingle();
            m.m21 = ReadSingle();
            m.m22 = ReadSingle();
            m.m23 = ReadSingle();
            m.m30 = ReadSingle();
            m.m31 = ReadSingle();
            m.m32 = ReadSingle();
            m.m33 = ReadSingle();
            return m;
        }

        public Guid ReadGuid()
        {
            byte[] bytes = reader.ReadBytes(16);
            return new Guid(bytes);
        }

        public Transform ReadTransform()
        {
            uint netId = ReadPackedUInt32();
            if (netId == 0)
            {
                return null;
            }

            NetworkIdentity identity;
            if (NetworkIdentity.spawned.TryGetValue(netId, out identity))
            {
                return identity.transform;
            }

            if (LogFilter.Debug) { Debug.Log("ReadTransform netId:" + netId + " not found in spawned"); }
            return null;
        }

        public GameObject ReadGameObject()
        {
            uint netId = ReadPackedUInt32();
            if (netId == 0)
            {
                return null;
            }

            NetworkIdentity identity;
            if (NetworkIdentity.spawned.TryGetValue(netId, out identity))
            {
                return identity.gameObject;
            }

            if (LogFilter.Debug) { Debug.Log("ReadGameObject netId:" + netId + " not found in spawned"); }
            return null;
        }

        public NetworkIdentity ReadNetworkIdentity()
        {
            uint netId = ReadPackedUInt32();
            if (netId == 0)
            {
                return null;
            }

            NetworkIdentity identity;
            if (NetworkIdentity.spawned.TryGetValue(netId, out identity))
            {
                return identity;
            }

            if (LogFilter.Debug) { Debug.Log("ReadNetworkIdentity netId:" + netId + " not found in spawned"); }
            return null;
        }

        public override string ToString()
        {
            return reader.ToString();
        }
    }
}
