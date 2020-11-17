using System;
using System.Linq;

namespace kcp2k
{
    public static partial class Utils
    {
        // ArraySegment content comparison
        public static bool SegmentsEqual(ArraySegment<byte> a, ArraySegment<byte> b)
        {
            // use Linq SequenceEqual. It doesn't allocate.
            return a.SequenceEqual(b);
        }
    }
}