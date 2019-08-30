using System.Runtime.CompilerServices;
using Mirror;


[assembly: InternalsVisibleTo("Mirror.Tests")]


[assembly: ReaderWriter(
    type: typeof(System.Int32),
    readerClass: typeof(NetworkReader),
    readerMethod: nameof(NetworkReader.ReadPackedInt32),
    writerClass: typeof(NetworkWriter),
    writerMethod: nameof(NetworkWriter.WritePackedInt32))]
