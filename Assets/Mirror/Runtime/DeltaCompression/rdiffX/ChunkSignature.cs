using System;

namespace Octodiff.Core
{
    public class ChunkSignature
    {
        public long StartOffset;            // 8 (but not included in the file on disk)
        public short Length;                // 2
        public byte[] Hash;                 // 20
        public uint RollingChecksum;        // 4
                                            // 26 bytes on disk
                                            // 34 bytes in memory

        public override string ToString()
        {
            return $"{StartOffset,6}:{Length,6} |{RollingChecksum,20}| {BitConverter.ToString(Hash).ToLowerInvariant().Replace("-", "")}";
        }
    }
}