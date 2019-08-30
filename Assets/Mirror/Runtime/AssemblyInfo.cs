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
    type: typeof(System.Int32),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadPackedInt32),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WritePackedInt32))]
