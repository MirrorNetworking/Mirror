// Grid3D based on Grid2D
// -> not named 'Grid' because Unity already has a Grid type. causes warnings.
// -> struct to avoid memory indirection. it's accessed a lot.
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    // struct to avoid memory indirection. it's accessed a lot.
    public struct Grid3D<T>
    {
        // the grid
        // note that we never remove old keys.
        // => over time, HashSet<T>s will be allocated for every possible
        //    grid position in the world
        // => Clear() doesn't clear them so we don't constantly reallocate the
        //    entries when populating the grid in every Update() call
        // => makes the code a lot easier too
        // => this is FINE because in the worst case, every grid position in the
        //    game world is filled with a player anyway!
        readonly Dictionary<Vector3Int, HashSet<T>> grid;

        // cache a 9 x 3 neighbor grid of vector3 offsets so we can use them more easily
        readonly Vector3Int[] neighbourOffsets;

        public Grid3D(int initialCapacity)
        {
            grid = new Dictionary<Vector3Int, HashSet<T>>(initialCapacity);

            neighbourOffsets = new Vector3Int[9 * 3];
            int i = 0;
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        neighbourOffsets[i] = new Vector3Int(x, y, z);
                        i += 1;
                    }
                }
            }
        }

        // helper function so we can add an entry without worrying
        public void Add(Vector3Int position, T value)
        {
            // initialize set in grid if it's not in there yet
            if (!grid.TryGetValue(position, out HashSet<T> hashSet))
            {
                // each grid entry may hold hundreds of entities.
                // let's create the HashSet with a large initial capacity
                // in order to avoid resizing & allocations.
#if !UNITY_2021_3_OR_NEWER
                // Unity 2019 doesn't have "new HashSet(capacity)" yet
                hashSet = new HashSet<T>();
#else
                hashSet = new HashSet<T>(128);
#endif
                grid[position] = hashSet;
            }

            // add to it
            hashSet.Add(value);
        }

        // helper function to get set at position without worrying
        // -> result is passed as parameter to avoid allocations
        // -> result is not cleared before. this allows us to pass the HashSet from
        //    GetWithNeighbours and avoid .UnionWith which is very expensive.
        void GetAt(Vector3Int position, HashSet<T> result)
        {
            // return the set at position
            if (grid.TryGetValue(position, out HashSet<T> hashSet))
            {
                foreach (T entry in hashSet)
                    result.Add(entry);
            }
        }

        // helper function to get at position and it's 8 neighbors without worrying
        // -> result is passed as parameter to avoid allocations
        public void GetWithNeighbours(Vector3Int position, HashSet<T> result)
        {
            // clear result first
            result.Clear();

            // add neighbours
            foreach (Vector3Int offset in neighbourOffsets)
                GetAt(position + offset, result);
        }

        // clear: clears the whole grid
        // IMPORTANT: we already allocated HashSet<T>s and don't want to do
        //            reallocate every single update when we rebuild the grid.
        //            => so simply remove each position's entries, but keep
        //               every position in there
        //            => see 'grid' comments above!
        //            => named ClearNonAlloc to make it more obvious!
        public void ClearNonAlloc()
        {
            foreach (HashSet<T> hashSet in grid.Values)
                hashSet.Clear();
        }
    }
}
