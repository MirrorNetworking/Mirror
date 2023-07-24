// HistoryBounds keeps a bounding box of all the object's bounds in the past N seconds.
// useful to decide which objects to rollback, instead of rolling back all of them.
// https://www.youtube.com/watch?v=zrIY0eIyqmI (37:00)
// standalone C# implementation to be engine (and language) agnostic.

using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public class HistoryBounds
    {
        public readonly Queue<Bounds> history; // TODO not public
        public int Count => history.Count;

        public readonly int limit;

        public HistoryBounds(int limit)
        {
            // initialize queue with maximum capacity to avoid runtime resizing
            this.limit = limit;
            history = new Queue<Bounds>(limit);
        }

        public void Reset()
        {
            history.Clear();
            // TODO reset total etc.
        }
    }

    public static class HistoryBoundsAlgo
    {
        // insert current bounds into history. returns new total bounds.
        // Queue.Dequeue() always has the oldest bounds.
        public static Bounds Insert(
            HistoryBounds history,
            Bounds bounds)
        {
            // optimization: only insert if

            // remove oldest if limit reached
            if (history.Count >= history.limit)
                history.history.Dequeue();

            // insert the new bounds
            history.history.Enqueue(bounds);

            // summarize total bounds.
            // starting at latest bounds, not at 'new Bounds' because that would
            // encapsulate (0,0) too.
            // TODO make this not be O(N)
            Bounds total = bounds;
            foreach (Bounds b in history.history)
                total.Encapsulate(b);

            return total;
        }
    }
}
