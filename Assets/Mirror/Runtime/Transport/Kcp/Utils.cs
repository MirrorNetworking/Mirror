using System;

namespace Mirror.KCP
{
    public static class Utils
    {
        public static int Clamp(int value, int lower, int upper)
        {
            return Math.Min(Math.Max(lower, value), upper);
        }

        public static bool Equal(ArraySegment<byte> seg1, ArraySegment<byte> seg2)
        {
            if (seg1.Count != seg2.Count)
                return false;

            for (int i=0; i< seg1.Count; i++)
            {
                if (seg1.Array[i + seg1.Offset] != seg2.Array[i+seg2.Offset])                
                    return false;
            }
            return true;
        }
    }
}
