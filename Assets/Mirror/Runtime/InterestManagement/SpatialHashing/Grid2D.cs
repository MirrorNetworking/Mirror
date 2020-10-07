// Grid2D from uMMORPG for spatial hashing.
// grid structure: get/set values of type T at any point
// -> not named 'Grid' because Unity already has a Grid type. causes warnings.
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public class Grid2D<T>
    {
        Dictionary<Vector2Int, HashSet<T>> grid = new Dictionary<Vector2Int, HashSet<T>>();

        // cache a 9 neighbor grid of vector2 offsets so we can use them more easily
        Vector2Int[] neighbourOffsets =
        {
            Vector2Int.up,
            Vector2Int.up + Vector2Int.left,
            Vector2Int.up + Vector2Int.right,
            Vector2Int.left,
            Vector2Int.zero,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.down + Vector2Int.left,
            Vector2Int.down + Vector2Int.right
        };

        // clear each position's hashset.
        // we don't just clear the dictionary because we don't want to reallocate
        // the HashSets each time.
        public void Clear()
        {
            foreach (HashSet<T> set in grid.Values)
            {
                set.Clear();
            }
        }

        // helper function so we can remove an entry without worrying
        public void Remove(Vector2Int position, T value)
        {
            // is this set in the grid? then remove it
            if (grid.TryGetValue(position, out HashSet<T> hashSet))
            {
                // remove value from this position's hashset
                hashSet.Remove(value);

                // if empty then remove this hashset entirely. no need to keep
                // HashSet<pos> in memory forever if no one is there anymore.
                if (hashSet.Count == 0)
                    grid.Remove(position);
            }
        }

        // helper function so we can add an entry without worrying
        public void Add(Vector2Int position, T value)
        {
            // initialize set in grid if it's not in there yet
            if (!grid.TryGetValue(position, out HashSet<T> hashSet))
            {
                hashSet = new HashSet<T>();
                grid[position] = hashSet;
            }

            // add to it
            hashSet.Add(value);
        }

        // helper function to get set at position without worrying
        // -> result is passed as parameter to avoid allocations
        // -> result is not cleared before. this allows us to pass the HashSet from
        //    GetWithNeighbours and avoid .UnionWith which is very expensive.
        void GetAt(Vector2Int position, HashSet<T> result)
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
        public void GetWithNeighbours(Vector2Int position, HashSet<T> result)
        {
            // note: we do NOT call result.Clear() first. the caller is responsible.
            // -> this allows us to pass .observers directly in RebuildObservers.
            foreach (Vector2Int offset in neighbourOffsets)
                GetAt(position + offset, result);
        }
    }
}
