using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Mirror
{
    public static class Extensions
    {
        public static string ToHexString(this ArraySegment<byte> segment) =>
            BitConverter.ToString(segment.Array, segment.Offset, segment.Count);

        // string.GetHashCode is not guaranteed to be the same on all
        // machines, but we need one that is the same on all machines.
        // NOTE: Do not call this from hot path because it's slow O(N) for long method names.
        // - As of 2012-02-16 There are 2 design-time callers (weaver) and 1 runtime caller that caches.
        public static int GetStableHashCode(this string text)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in text)
                    hash = hash * 31 + c;

                //UnityEngine.Debug.Log($"Created stable hash {(ushort)hash} for {text}");
                return hash;
            }
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
    }
}
