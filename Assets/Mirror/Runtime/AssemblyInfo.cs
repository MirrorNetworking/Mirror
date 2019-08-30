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
    type: typeof(System.Int32),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadPackedInt32),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WritePackedInt32))]
