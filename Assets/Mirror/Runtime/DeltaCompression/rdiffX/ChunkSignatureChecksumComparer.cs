using System.Collections.Generic;

namespace Octodiff.Core
{
    internal class ChunkSignatureChecksumComparer : IComparer<ChunkSignature>
    {
        public int Compare(ChunkSignature x, ChunkSignature y)
        {
            int comparison = x.RollingChecksum.CompareTo(y.RollingChecksum);
            return comparison == 0 ? x.StartOffset.CompareTo(y.StartOffset) : comparison;
        }
    }
}