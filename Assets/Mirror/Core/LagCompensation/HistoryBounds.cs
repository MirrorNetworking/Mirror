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

        public Bounds total;

        public HistoryBounds(int limit)
        {
            // initialize queue with maximum capacity to avoid runtime resizing
            this.limit = limit;
            history = new Queue<Bounds>(limit);
        }

        // insert new bounds into history. calculates new total bounds.
        // Queue.Dequeue() always has the oldest bounds.
        public void Insert(Bounds bounds)
        {
            // remove oldest if limit reached
            if (history.Count >= limit)
                history.Dequeue();

            // insert the new bounds
            history.Enqueue(bounds);

            // summarize total bounds.
            // starting at latest bounds, not at 'new Bounds' because that would
            // encapsulate (0,0) too.
            // TODO make this not be O(N)
            total = bounds;
            foreach (Bounds b in history)
                total.Encapsulate(b);
        }

        public void Reset()
        {
            history.Clear();
            total = new Bounds();
        }
    }
}
