using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror
{
    public static class Extensions
    {
        public static string ToHexString(this ArraySegment<byte> segment) =>
            BitConverter.ToString(segment.Array, segment.Offset, segment.Count);

        // string.GetHashCode is not guaranteed to be the same on all
        // machines, but we need one that is the same on all machines.
        // Uses fnv1a as hash function for more uniform distribution http://www.isthe.com/chongo/tech/comp/fnv/
        // Tests: https://softwareengineering.stackexchange.com/questions/49550/which-hashing-algorithm-is-best-for-uniqueness-and-speed
        // NOTE: Do not call this from hot path because it's slow O(N) for long method names.
        // - As of 2012-02-16 There are 2 design-time callers (weaver) and 1 runtime caller that caches.
        public static int GetStableHashCode(this string text)
        {
            unchecked
            {
                uint hash = 0x811c9dc5;
                uint prime = 0x1000193;

                for (int i = 0; i < text.Length; ++i)
                {
                    byte value = (byte)text[i];
                    hash = hash ^ value;
                    hash *= prime;
                }

                //UnityEngine.Debug.Log($"Created stable hash {(ushort)hash} for {text}");
                return (int)hash;
            }
        }

        // smaller version of our GetStableHashCode.
        // careful, this significantly increases chance of collisions.
        public static ushort GetStableHashCode16(this string text)
        {
            // deterministic hash
            int hash = GetStableHashCode(text);

            // Gets the 32bit fnv1a hash
            // To get it down to 16bit but still reduce hash collisions we cant just cast it to ushort
            // Instead we take the highest 16bits of the 32bit hash and fold them with xor into the lower 16bits
            // This will create a more uniform 16bit hash, the method is described in:
            // http://www.isthe.com/chongo/tech/comp/fnv/ in section "Changing the FNV hash size - xor-folding"
            return (ushort)((hash >> 16) ^ hash);
        }

        // previously in DotnetCompatibility.cs
        // leftover from the UNET days. supposedly for windows store?
        internal static string GetMethodName(this Delegate func)
        {
#if NETFX_CORE
            return func.GetMethodInfo().Name;
#else
            return func.Method.Name;
#endif
        }

        // helper function to copy to List<T>
        // C# only provides CopyTo(T[])
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyTo<T>(this IEnumerable<T> source, List<T> destination)
        {
            // foreach allocates. use AddRange.
            destination.AddRange(source);
        }

#if !UNITY_2021_OR_NEWER
        // Unity 2020 and earlier don't have Queue.TryDequeue which we need for batching.
        public static bool TryDequeue<T>(this Queue<T> source, out T element)
        {
            if (source.Count > 0)
            {
                element = source.Dequeue();
                return true;
            }

            element = default;
            return false;
        }
#endif

#if !UNITY_2021_OR_NEWER
        // Unity 2020 and earlier don't have ConcurrentQueue.Clear which we need for ThreadedTransport.
        public static void Clear<T>(this ConcurrentQueue<T> source)
        {
            // while count > 0 risks deadlock if other thread write at the same time.
            // our safest solution is a best-effort approach to clear 'Count' once.
            int count = source.Count; // get it only once
            for (int i = 0; i < count; ++i)
            {
                source.TryDequeue(out _);
            }
        }
#endif

#if !UNITY_2021_3_OR_NEWER
        // Unity 2021.2 and earlier don't have transform.GetPositionAndRotation which we use for performance in some places
        public static void GetPositionAndRotation(this Transform transform, out Vector3 position, out Quaternion rotation)
        {
            position = transform.position;
            rotation = transform.rotation;
        }
#endif
    }
}
