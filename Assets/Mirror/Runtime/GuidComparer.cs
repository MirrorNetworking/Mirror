using System.Collections.Generic;
namespace Mirror
{
    public class GuidComparer : IEqualityComparer<byte[]>
    {
        public const int length = 16;
        public int GetHashCode(byte[] b)
        {
            return (b[3] << 24) | (b[2] << 16) | (b[1] << 8) | b[0];
        }

        public bool Equals(byte[] a, byte[] b)
        {
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }
}