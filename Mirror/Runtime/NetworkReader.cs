using System;
using System.IO;
using UnityEngine;

namespace Mirror
{
    public class NetworkReader
    {
        BinaryReader reader;

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
                ushort size = ReadUInt16();
                return reader.ReadBytes(size);
            }
            return null;
        }

        // http://sqlite.org/src4/doc/trunk/www/varint.wiki
        // NOTE: big endian.
        public UInt32 ReadPackedUInt32()
        {
            byte a0 = ReadByte();
            if (a0 < 241)
            {
                return a0;
            }
            byte a1 = ReadByte();
            if (a0 >= 241 && a0 <= 248)
            {
                return (UInt32)(240 + 256 * (a0 - 241) + a1);
            }
            byte a2 = ReadByte();
            if (a0 == 249)
            {
                return (UInt32)(2288 + 256 * a1 + a2);
            }
            byte a3 = ReadByte();
            if (a0 == 250)
            {
                return a1 + (((UInt32)a2) << 8) + (((UInt32)a3) << 16);
            }
            byte a4 = ReadByte();
            if (a0 >= 251)
            {
                return a1 + (((UInt32)a2) << 8) + (((UInt32)a3) << 16) + (((UInt32)a4) << 24);
            }
            throw new IndexOutOfRangeException("ReadPackedUInt32() failure: " + a0);
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

        public NetworkInstanceId ReadNetworkId()
        {
            return new NetworkInstanceId(ReadPackedUInt32());
        }

        public NetworkSceneId ReadSceneId()
        {
            return new NetworkSceneId(ReadPackedUInt32());
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

        public NetworkHash128 ReadNetworkHash128()
        {
            NetworkHash128 hash;
            hash.i0 = ReadByte();
            hash.i1 = ReadByte();
            hash.i2 = ReadByte();
            hash.i3 = ReadByte();
            hash.i4 = ReadByte();
            hash.i5 = ReadByte();
            hash.i6 = ReadByte();
            hash.i7 = ReadByte();
            hash.i8 = ReadByte();
            hash.i9 = ReadByte();
            hash.i10 = ReadByte();
            hash.i11 = ReadByte();
            hash.i12 = ReadByte();
            hash.i13 = ReadByte();
            hash.i14 = ReadByte();
            hash.i15 = ReadByte();
            return hash;
        }

        public Transform ReadTransform()
        {
            NetworkInstanceId netId = ReadNetworkId();
            if (netId.IsEmpty())
            {
                return null;
            }
            GameObject go = ClientScene.FindLocalObject(netId);
            if (go == null)
            {
                if (LogFilter.logDebug) { Debug.Log("ReadTransform netId:" + netId); }
                return null;
            }

            return go.transform;
        }

        public GameObject ReadGameObject()
        {
            NetworkInstanceId netId = ReadNetworkId();
            if (netId.IsEmpty())
            {
                return null;
            }

            GameObject go;
            if (NetworkServer.active)
            {
                go = NetworkServer.FindLocalObject(netId);
            }
            else
            {
                go = ClientScene.FindLocalObject(netId);
            }
            if (go == null)
            {
                if (LogFilter.logDebug) { Debug.Log("ReadGameObject netId:" + netId + "go: null"); }
            }

            return go;
        }

        public NetworkIdentity ReadNetworkIdentity()
        {
            NetworkInstanceId netId = ReadNetworkId();
            if (netId.IsEmpty())
            {
                return null;
            }
            GameObject go;
            if (NetworkServer.active)
            {
                go = NetworkServer.FindLocalObject(netId);
            }
            else
            {
                go = ClientScene.FindLocalObject(netId);
            }
            if (go == null)
            {
                if (LogFilter.logDebug) { Debug.Log("ReadNetworkIdentity netId:" + netId + "go: null"); }
                return null;
            }

            return go.GetComponent<NetworkIdentity>();
        }

        public override string ToString()
        {
            return reader.ToString();
        }

        public TMsg ReadMessage<TMsg>() where TMsg : MessageBase, new()
        {
            var msg = new TMsg();
            msg.Deserialize(this);
            return msg;
        }
    };
}
