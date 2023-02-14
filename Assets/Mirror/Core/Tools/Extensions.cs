using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Mirror
{
    public static class Extensions
    {
        public static string ToHexString(this ArraySegment<byte> segment) =>
            BitConverter.ToString(segment.Array, segment.Offset, segment.Count);

        // GetStableHashCode is O(N).
        // the longer the string, the more it needs to compute:
        // https://github.com/MirrorNetworking/Mirror/pull/3377
        // cache results for O(1) lookups.
        static readonly Dictionary<string, int> StableHashes = new Dictionary<string, int>();

        [UnityEngine.RuntimeInitializeOnLoadMethod]
        public static void ResetStatics()
        {
            StableHashes.Clear();
        }

        // string.GetHashCode is not guaranteed to be the same on all machines, but
        // we need one that is the same on all machines. simple and stupid:
        public static int GetStableHashCode(this string text)
        {
            if (StableHashes.TryGetValue(text, out int cachedHash))
                return cachedHash;

            unchecked
            {
                int hash = 23;
                foreach (char c in text)
                    hash = hash * 31 + c;

                //UnityEngine.Debug.Log($"Caching stable hash {(ushort)hash} for {text}");
                StableHashes[text] = hash;
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
        // Unity 2020 and earlier doesn't have Queue.TryDequeue which we need for batching.
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
    }
}
