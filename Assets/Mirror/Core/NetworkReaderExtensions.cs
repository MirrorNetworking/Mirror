using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mirror
{
    // Mirror's Weaver automatically detects all NetworkReader function types,
    // but they do all need to be extensions.
    public static class NetworkReaderExtensions
    {
        public static byte ReadByte(this NetworkReader reader) => reader.ReadBlittable<byte>();
        public static byte? ReadByteNullable(this NetworkReader reader) => reader.ReadBlittableNullable<byte>();

        public static sbyte ReadSByte(this NetworkReader reader) => reader.ReadBlittable<sbyte>();
        public static sbyte? ReadSByteNullable(this NetworkReader reader) => reader.ReadBlittableNullable<sbyte>();

        // bool is not blittable. read as ushort.
        public static char ReadChar(this NetworkReader reader) => (char)reader.ReadBlittable<ushort>();
        public static char? ReadCharNullable(this NetworkReader reader) => (char?)reader.ReadBlittableNullable<ushort>();

        // bool is not blittable. read as byte.
        public static bool ReadBool(this NetworkReader reader) => reader.ReadBlittable<byte>() != 0;
        public static bool? ReadBoolNullable(this NetworkReader reader)
        {
            byte? value = reader.ReadBlittableNullable<byte>();
            return value.HasValue ? (value.Value != 0) : default(bool?);
        }

        public static short ReadShort(this NetworkReader reader) => (short)reader.ReadUShort();
        public static short? ReadShortNullable(this NetworkReader reader) => reader.ReadBlittableNullable<short>();

        public static ushort ReadUShort(this NetworkReader reader) => reader.ReadBlittable<ushort>();
        public static ushort? ReadUShortNullable(this NetworkReader reader) => reader.ReadBlittableNullable<ushort>();

        public static int ReadInt(this NetworkReader reader) => reader.ReadBlittable<int>();
        public static int? ReadIntNullable(this NetworkReader reader) => reader.ReadBlittableNullable<int>();

        public static uint ReadUInt(this NetworkReader reader) => reader.ReadBlittable<uint>();
        public static uint? ReadUIntNullable(this NetworkReader reader) => reader.ReadBlittableNullable<uint>();

        public static long ReadLong(this NetworkReader reader) => reader.ReadBlittable<long>();
        public static long? ReadLongNullable(this NetworkReader reader) => reader.ReadBlittableNullable<long>();

        public static ulong ReadULong(this NetworkReader reader) => reader.ReadBlittable<ulong>();
        public static ulong? ReadULongNullable(this NetworkReader reader) => reader.ReadBlittableNullable<ulong>();

        // ReadInt/UInt/Long/ULong writes full bytes by default.
        // define additional "VarInt" versions that Weaver will automatically prefer.
        // 99% of the time [SyncVar] ints are small values, which makes this very much worth it.
        [WeaverPriority] public static int ReadVarInt(this NetworkReader reader) => (int)Compression.DecompressVarInt(reader);
        [WeaverPriority] public static uint ReadVarUInt(this NetworkReader reader) => (uint)Compression.DecompressVarUInt(reader);
        [WeaverPriority] public static long ReadVarLong(this NetworkReader reader) => Compression.DecompressVarInt(reader);
        [WeaverPriority] public static ulong ReadVarULong(this NetworkReader reader) => Compression.DecompressVarUInt(reader);

        public static float ReadFloat(this NetworkReader reader) => reader.ReadBlittable<float>();
        public static float? ReadFloatNullable(this NetworkReader reader) => reader.ReadBlittableNullable<float>();

        public static double ReadDouble(this NetworkReader reader) => reader.ReadBlittable<double>();
        public static double? ReadDoubleNullable(this NetworkReader reader) => reader.ReadBlittableNullable<double>();

        public static decimal ReadDecimal(this NetworkReader reader) => reader.ReadBlittable<decimal>();
        public static decimal? ReadDecimalNullable(this NetworkReader reader) => reader.ReadBlittableNullable<decimal>();

        public static Half ReadHalf(this NetworkReader reader) => new Half(reader.ReadUShort());

        /// <exception cref="T:System.ArgumentException">if an invalid utf8 string is sent</exception>
        public static string ReadString(this NetworkReader reader)
        {
            // read number of bytes
            ushort size = reader.ReadUShort();

            // null support, see NetworkWriter
            if (size == 0)
                return null;

            ushort realSize = (ushort)(size - 1);

            // make sure it's within limits to avoid allocation attacks etc.
            if (realSize > NetworkWriter.MaxStringLength)
                throw new EndOfStreamException($"NetworkReader.ReadString - Value too long: {realSize} bytes. Limit is: {NetworkWriter.MaxStringLength} bytes");

            ArraySegment<byte> data = reader.ReadBytesSegment(realSize);

            // convert directly from buffer to string via encoding
            // throws in case of invalid utf8.
            // see test: ReadString_InvalidUTF8()
            return reader.encoding.GetString(data.Array, data.Offset, data.Count);
        }

        public static byte[] ReadBytes(this NetworkReader reader, int count)
        {
            // prevent allocation attacks with a reasonable limit.
            //   server shouldn't allocate too much on client devices.
            //   client shouldn't allocate too much on server in ClientToServer [SyncVar]s.
            if (count > NetworkReader.AllocationLimit)
            {
                // throw EndOfStream for consistency with ReadBlittable when out of data
                throw new EndOfStreamException($"NetworkReader attempted to allocate {count} bytes, which is larger than the allowed limit of {NetworkReader.AllocationLimit} bytes.");
            }

            byte[] bytes = new byte[count];
            reader.ReadBytes(bytes, count);
            return bytes;
        }

        /// <exception cref="T:OverflowException">if count is invalid</exception>
        public static byte[] ReadBytesAndSize(this NetworkReader reader)
        {
            // we offset count by '1' to easily support null without writing another byte.
            // encoding null as '0' instead of '-1' also allows for better compression
            // (ushort vs. short / varuint vs. varint) etc.

            // most sizes are small, read size as VarUInt!
            uint count = (uint)Compression.DecompressVarUInt(reader);
            // uint count = reader.ReadUInt();
            // Use checked() to force it to throw OverflowException if data is invalid
            return count == 0 ? null : reader.ReadBytes(checked((int)(count - 1u)));
        }
        // Reads ArraySegment and size header
        /// <exception cref="T:OverflowException">if count is invalid</exception>
        public static ArraySegment<byte> ReadArraySegmentAndSize(this NetworkReader reader)
        {
            // we offset count by '1' to easily support null without writing another byte.
            // encoding null as '0' instead of '-1' also allows for better compression
            // (ushort vs. short / varuint vs. varint) etc.

            // most sizes are small, read size as VarUInt!
            uint count = (uint)Compression.DecompressVarUInt(reader);
            // uint count = reader.ReadUInt();
            // Use checked() to force it to throw OverflowException if data is invalid
            return count == 0 ? default : reader.ReadBytesSegment(checked((int)(count - 1u)));
        }

        public static Vector2 ReadVector2(this NetworkReader reader) => reader.ReadBlittable<Vector2>();
        public static Vector2? ReadVector2Nullable(this NetworkReader reader) => reader.ReadBlittableNullable<Vector2>();

        public static Vector3 ReadVector3(this NetworkReader reader) => reader.ReadBlittable<Vector3>();
        public static Vector3? ReadVector3Nullable(this NetworkReader reader) => reader.ReadBlittableNullable<Vector3>();

        public static Vector4 ReadVector4(this NetworkReader reader) => reader.ReadBlittable<Vector4>();
        public static Vector4? ReadVector4Nullable(this NetworkReader reader) => reader.ReadBlittableNullable<Vector4>();

        public static Vector2Int ReadVector2Int(this NetworkReader reader) => reader.ReadBlittable<Vector2Int>();
        public static Vector2Int? ReadVector2IntNullable(this NetworkReader reader) => reader.ReadBlittableNullable<Vector2Int>();

        public static Vector3Int ReadVector3Int(this NetworkReader reader) => reader.ReadBlittable<Vector3Int>();
        public static Vector3Int? ReadVector3IntNullable(this NetworkReader reader) => reader.ReadBlittableNullable<Vector3Int>();

        public static Color ReadColor(this NetworkReader reader) => reader.ReadBlittable<Color>();
        public static Color? ReadColorNullable(this NetworkReader reader) => reader.ReadBlittableNullable<Color>();

        public static Color32 ReadColor32(this NetworkReader reader) => reader.ReadBlittable<Color32>();
        public static Color32? ReadColor32Nullable(this NetworkReader reader) => reader.ReadBlittableNullable<Color32>();

        public static Quaternion ReadQuaternion(this NetworkReader reader) => reader.ReadBlittable<Quaternion>();
        public static Quaternion? ReadQuaternionNullable(this NetworkReader reader) => reader.ReadBlittableNullable<Quaternion>();

        // Rect is a struct with properties instead of fields
        public static Rect ReadRect(this NetworkReader reader) => new Rect(reader.ReadVector2(), reader.ReadVector2());
        public static Rect? ReadRectNullable(this NetworkReader reader) => reader.ReadBool() ? ReadRect(reader) : default(Rect?);

        // Plane is a struct with properties instead of fields
        public static Plane ReadPlane(this NetworkReader reader) => new Plane(reader.ReadVector3(), reader.ReadFloat());
        public static Plane? ReadPlaneNullable(this NetworkReader reader) => reader.ReadBool() ? ReadPlane(reader) : default(Plane?);

        // Ray is a struct with properties instead of fields
        public static Ray ReadRay(this NetworkReader reader) => new Ray(reader.ReadVector3(), reader.ReadVector3());
        public static Ray? ReadRayNullable(this NetworkReader reader) => reader.ReadBool() ? ReadRay(reader) : default(Ray?);

        // LayerMask is a struct with properties instead of fields
        public static LayerMask ReadLayerMask(this NetworkReader reader)
        {
            // LayerMask doesn't have a constructor that takes an initial value.
            // 32 layers as a flags enum, max value of 496, we only need a UShort.
            LayerMask layerMask = default;
            layerMask.value = reader.ReadUShort();
            return layerMask;
        }

        public static LayerMask? ReadLayerMaskNullable(this NetworkReader reader) => reader.ReadBool() ? ReadLayerMask(reader) : default(LayerMask?);

        public static Matrix4x4 ReadMatrix4x4(this NetworkReader reader) => reader.ReadBlittable<Matrix4x4>();
        public static Matrix4x4? ReadMatrix4x4Nullable(this NetworkReader reader) => reader.ReadBlittableNullable<Matrix4x4>();

        public static Guid ReadGuid(this NetworkReader reader)
        {
#if !UNITY_2021_3_OR_NEWER
            // Unity 2019 doesn't have Span yet
            return new Guid(reader.ReadBytes(16));
#else
            // ReadBlittable(Guid) isn't safe. see ReadBlittable comments.
            // Guid is Sequential, but we can't guarantee packing.
            if (reader.Remaining >= 16)
            {
                ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(reader.buffer.Array, reader.buffer.Offset + reader.Position, 16);
                reader.Position += 16;
                return new Guid(span);
            }
            throw new EndOfStreamException($"ReadGuid out of range: {reader}");
#endif
        }
        public static Guid? ReadGuidNullable(this NetworkReader reader) => reader.ReadBool() ? ReadGuid(reader) : default(Guid?);

        public static NetworkIdentity ReadNetworkIdentity(this NetworkReader reader)
        {
            uint netId = reader.ReadUInt();
            if (netId == 0)
                return null;

            // NOTE: a netId not being in spawned is common.
            // for example, "[SyncVar] NetworkIdentity target" netId would not
            // be known on client if the monster walks out of proximity for a
            // moment. no need to log any error or warning here.
            return Utils.GetSpawnedInServerOrClient(netId);
        }

        public static NetworkBehaviour ReadNetworkBehaviour(this NetworkReader reader)
        {
            // read netId first.
            //
            // IMPORTANT: if netId != 0, writer always writes componentIndex.
            //   reusing ReadNetworkIdentity() might return a null NetworkIdentity
            //   even if netId was != 0 but the identity disappeared on the client,
            //   resulting in unequal amounts of data being written / read.
            //   https://github.com/vis2k/Mirror/issues/2972
            uint netId = reader.ReadUInt();
            if (netId == 0)
                return null;

            // read component index in any case, BEFORE searching the spawned
            // NetworkIdentity by netId.
            byte componentIndex = reader.ReadByte();

            // NOTE: a netId not being in spawned is common.
            // for example, "[SyncVar] NetworkIdentity target" netId would not
            // be known on client if the monster walks out of proximity for a
            // moment. no need to log any error or warning here.
            NetworkIdentity identity = Utils.GetSpawnedInServerOrClient(netId);

            return identity != null
                ? identity.NetworkBehaviours[componentIndex]
                : null;
        }

        public static T ReadNetworkBehaviour<T>(this NetworkReader reader) where T : NetworkBehaviour
        {
            return reader.ReadNetworkBehaviour() as T;
        }

        public static NetworkBehaviourSyncVar ReadNetworkBehaviourSyncVar(this NetworkReader reader)
        {
            uint netId = reader.ReadUInt();
            byte componentIndex = default;

            // if netId is not 0, then index is also sent to read before returning
            if (netId != 0)
            {
                componentIndex = reader.ReadByte();
            }

            return new NetworkBehaviourSyncVar(netId, componentIndex);
        }

        public static Transform ReadTransform(this NetworkReader reader)
        {
            // Don't use null propagation here as it could lead to MissingReferenceException
            NetworkIdentity networkIdentity = reader.ReadNetworkIdentity();
            return networkIdentity != null ? networkIdentity.transform : null;
        }

        public static GameObject ReadGameObject(this NetworkReader reader)
        {
            // Don't use null propagation here as it could lead to MissingReferenceException
            NetworkIdentity networkIdentity = reader.ReadNetworkIdentity();
            return networkIdentity != null ? networkIdentity.gameObject : null;
        }

        // while SyncList<T> is recommended for NetworkBehaviours,
        // structs may have .List<T> members which weaver needs to be able to
        // fully serialize for NetworkMessages etc.
        // note that Weaver/Readers/GenerateReader() handles this manually.
        public static List<T> ReadList<T>(this NetworkReader reader)
        {
            // we offset count by '1' to easily support null without writing another byte.
            // encoding null as '0' instead of '-1' also allows for better compression
            // (ushort vs. short / varuint vs. varint) etc.

            // most sizes are small, read size as VarUInt!
            uint length = (uint)Compression.DecompressVarUInt(reader);
            // uint length = reader.ReadUInt();
            if (length == 0) return null;
            length -= 1;

            // prevent allocation attacks with a reasonable limit.
            //   server shouldn't allocate too much on client devices.
            //   client shouldn't allocate too much on server in ClientToServer [SyncVar]s.
            if (length > NetworkReader.AllocationLimit)
            {
                // throw EndOfStream for consistency with ReadBlittable when out of data
                throw new EndOfStreamException($"NetworkReader attempted to allocate a List<{typeof(T)}> {length} elements, which is larger than the allowed limit of {NetworkReader.AllocationLimit}.");
            }

            List<T> result = new List<T>((checked((int)length)));
            for (int i = 0; i < length; i++)
            {
                result.Add(reader.Read<T>());
            }
            return result;
        }

        // while SyncSet<T> is recommended for NetworkBehaviours,
        // structs may have .Set<T> members which weaver needs to be able to
        // fully serialize for NetworkMessages etc.
        // note that Weaver/Readers/GenerateReader() handles this manually.
        public static HashSet<T> ReadHashSet<T>(this NetworkReader reader)
        {
            // we offset count by '1' to easily support null without writing another byte.
            // encoding null as '0' instead of '-1' also allows for better compression
            // (ushort vs. short / varuint vs. varint) etc.

            // most sizes are small, read size as VarUInt!
            uint length = (uint)Compression.DecompressVarUInt(reader);
            //uint length = reader.ReadUInt();
            if (length == 0) return null;
            length -= 1;

            HashSet<T> result = new HashSet<T>();
            for (int i = 0; i < length; i++)
            {
                result.Add(reader.Read<T>());
            }
            return result;
        }

        public static T[] ReadArray<T>(this NetworkReader reader)
        {
            // we offset count by '1' to easily support null without writing another byte.
            // encoding null as '0' instead of '-1' also allows for better compression
            // (ushort vs. short / varuint vs. varint) etc.

            // most sizes are small, read size as VarUInt!
            uint length = (uint)Compression.DecompressVarUInt(reader);
            //uint length = reader.ReadUInt();
            if (length == 0) return null;
            length -= 1;

            // prevent allocation attacks with a reasonable limit.
            //   server shouldn't allocate too much on client devices.
            //   client shouldn't allocate too much on server in ClientToServer [SyncVar]s.
            if (length > NetworkReader.AllocationLimit)
            {
                // throw EndOfStream for consistency with ReadBlittable when out of data
                throw new EndOfStreamException($"NetworkReader attempted to allocate an Array<{typeof(T)}> with {length} elements, which is larger than the allowed limit of {NetworkReader.AllocationLimit}.");
            }

            // we can't check if reader.Remaining < length,
            // because we don't know sizeof(T) since it's a managed type.
            // if (length > reader.Remaining) throw new EndOfStreamException($"Received array that is too large: {length}");

            T[] result = new T[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = reader.Read<T>();
            }
            return result;
        }

        public static Uri ReadUri(this NetworkReader reader)
        {
            string uriString = reader.ReadString();
            return (string.IsNullOrWhiteSpace(uriString) ? null : new Uri(uriString));
        }

        public static Texture2D ReadTexture2D(this NetworkReader reader)
        {
            // support 'null' textures for [SyncVar]s etc.
            // https://github.com/vis2k/Mirror/issues/3144
            short width = reader.ReadShort();
            if (width == -1) return null;

            // read height
            short height = reader.ReadShort();

            // prevent allocation attacks with a reasonable limit.
            //   server shouldn't allocate too much on client devices.
            //   client shouldn't allocate too much on server in ClientToServer [SyncVar]s.
            // log an error and return default.
            // we don't want attackers to be able to trigger exceptions.
            int totalSize = width * height;
            if (totalSize > NetworkReader.AllocationLimit)
            {
                Debug.LogWarning($"NetworkReader attempted to allocate a Texture2D with total size (width * height) of {totalSize}, which is larger than the allowed limit of {NetworkReader.AllocationLimit}.");
                return null;
            }

            Texture2D texture2D = new Texture2D(width, height);

            // read pixel content
            Color32[] pixels = reader.ReadArray<Color32>();
            texture2D.SetPixels32(pixels);
            texture2D.Apply();
            return texture2D;
        }

        public static Sprite ReadSprite(this NetworkReader reader)
        {
            // support 'null' textures for [SyncVar]s etc.
            // https://github.com/vis2k/Mirror/issues/3144
            Texture2D texture = reader.ReadTexture2D();
            if (texture == null) return null;

            // otherwise create a valid sprite
            return Sprite.Create(texture, reader.ReadRect(), reader.ReadVector2());
        }

        public static DateTime ReadDateTime(this NetworkReader reader) => DateTime.FromOADate(reader.ReadDouble());
        public static DateTime? ReadDateTimeNullable(this NetworkReader reader) => reader.ReadBool() ? ReadDateTime(reader) : default(DateTime?);
    }
}
