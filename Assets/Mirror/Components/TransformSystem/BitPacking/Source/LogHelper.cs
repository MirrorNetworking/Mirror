using System;
using UnityEngine;

namespace JamesFrowen.BitPacking
{
    public static class LogHelper
    {
        public static void LogBits(uint n)
        {
            var builder = new System.Text.StringBuilder();
            for (var i = sizeof(uint) * 8 - 1; i >= 0; i--)
            {
                var shift = n >> i;
                var masked = shift & 0b1;
                builder.Append(masked);
            }

            Debug.Log(builder.ToString());
        }

        public static void LogBits(ulong n)
        {
            var builder = new System.Text.StringBuilder();
            for (var i = sizeof(ulong) * 8 - 1; i >= 0; i--)
            {
                var shift = n >> i;
                var masked = shift & 0b1;
                builder.Append(masked);
            }

            Debug.Log(builder.ToString());
        }

        public static void LogHex(ArraySegment<byte> segment)
            => LogHex(segment.Array, segment.Offset, segment.Count);

        public static void LogHex(byte[] bytes, int? offset = null, int? count = null)
        {
            Debug.Log(BitConverter.ToString(bytes, offset ?? 0, count ?? bytes.Length));
        }
    }
}
