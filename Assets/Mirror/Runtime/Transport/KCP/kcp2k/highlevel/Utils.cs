using System;
using Unity.Collections.LowLevel.Unsafe;

namespace kcp2k
{
    public static partial class Utils
    {
        // ArraySegment content comparison without Linq
        public static unsafe bool SegmentsEqual(ArraySegment<byte> a, ArraySegment<byte> b)
        {
            if (a.Count == b.Count)
            {
                fixed (byte* aPtr = &a.Array[a.Offset],
                             bPtr = &b.Array[b.Offset])
                {
                    return UnsafeUtility.MemCmp(aPtr, bPtr, a.Count) == 0;
                }
            }
            return false;
        }

    }
}