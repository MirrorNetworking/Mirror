using System.IO;

namespace Octodiff.Core
{
    public interface IDeltaWriter
    {
        void WriteMetadata(byte[] expectedNewFileHash);
        void WriteCopyCommand(DataRange segment);
        void WriteDataCommand(Stream source, long offset, long length);
        void Finish();
    }
}