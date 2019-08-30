using System.Runtime.CompilerServices;
using Mirror;


[assembly: InternalsVisibleTo("Mirror.Tests")]

[assembly: ReaderWriter(
    type: typeof(System.Single),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadSingle),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteSingle))]

[assembly: ReaderWriter(
    type: typeof(System.Double),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadDouble),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteDouble))]

[assembly: ReaderWriter(
    type: typeof(System.Boolean),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadBoolean),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteBoolean))]

[assembly: ReaderWriter(
    type: typeof(System.String),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadString),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteString))]

[assembly: ReaderWriter(
    type: typeof(System.Int64),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadPackedInt64),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WritePackedInt64))]

[assembly: ReaderWriter(
    type: typeof(System.UInt64),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadPackedUInt64),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WritePackedUInt64))]

[assembly: ReaderWriter(
    type: typeof(System.Int16),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadInt16),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteInt16))]

[assembly: ReaderWriter(
    type: typeof(System.UInt16),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadUInt16),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteUInt16))]

[assembly: ReaderWriter(
    type: typeof(System.Byte),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadByte),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteByte))]

[assembly: ReaderWriter(
    type: typeof(System.SByte),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadSByte),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteSByte))]

[assembly: ReaderWriter(
    type: typeof(System.Char),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadChar),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteChar))]

[assembly: ReaderWriter(
    type: typeof(System.Decimal),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadDecimal),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteDecimal))]

[assembly: ReaderWriter(
    type: typeof(UnityEngine.Vector2),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadVector2),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteVector2))]

[assembly: ReaderWriter(
    type: typeof(UnityEngine.Vector3),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadVector3),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteVector3))]

[assembly: ReaderWriter(
    type: typeof(UnityEngine.Vector4),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadVector4),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteVector4))]

[assembly: ReaderWriter(
    type: typeof(UnityEngine.Vector2Int),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadVector2Int),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteVector2Int))]

[assembly: ReaderWriter(
    type: typeof(UnityEngine.Vector3Int),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadVector3Int),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteVector3Int))]

[assembly: ReaderWriter(
    type: typeof(UnityEngine.Color),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadColor),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteColor))]

[assembly: ReaderWriter(
    type: typeof(UnityEngine.Color32),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadColor32),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteColor32))]

[assembly: ReaderWriter(
    type: typeof(UnityEngine.Quaternion),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadQuaternion),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteQuaternion))]

[assembly: ReaderWriter(
    type: typeof(UnityEngine.Rect),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadRect),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteRect))]

[assembly: ReaderWriter(
    type: typeof(UnityEngine.Plane),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadPlane),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WritePlane))]

[assembly: ReaderWriter(
    type: typeof(UnityEngine.Ray),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadRay),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteRay))]

[assembly: ReaderWriter(
    type: typeof(UnityEngine.Matrix4x4),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadMatrix4x4),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteMatrix4x4))]

[assembly: ReaderWriter(
    type: typeof(System.Guid),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadGuid),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteGuid))]

[assembly: ReaderWriter(
    type: typeof(UnityEngine.GameObject),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadGameObject),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteGameObject))]

[assembly: ReaderWriter(
    type: typeof(Mirror.NetworkIdentity),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadNetworkIdentity),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteNetworkIdentity))]

[assembly: ReaderWriter(
    type: typeof(UnityEngine.Transform),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadTransform),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WriteTransform))]

[assembly: ReaderWriter(
    type: typeof(System.UInt32),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadPackedUInt32),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WritePackedUInt64))]

[assembly: ReaderWriter(
    type: typeof(System.Int32),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadPackedInt32),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WritePackedInt32))]
