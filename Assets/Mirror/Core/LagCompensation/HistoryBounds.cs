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
        // history of bounds
        readonly Queue<Bounds> history;
        public int Count => history.Count;

        // history limit. oldest bounds will be removed.
        public readonly int limit;

        // total bounds encapsulating all of the bounds history
        public Bounds total;

        public HistoryBounds(int limit)
        {
            // initialize queue with maximum capacity to avoid runtime resizing
            // +1 because it makes the code easier if we insert first, and then remove.
            this.limit = limit;
            history = new Queue<Bounds>(limit + 1);
        }

        // insert new bounds into history. calculates new total bounds.
        // Queue.Dequeue() always has the oldest bounds.
        public void Insert(Bounds bounds)
        {
            // initialize 'total' if not initialized yet.
            // we don't want to call (0,0).Encapsulate(bounds).
            if (history.Count == 0)
                total = bounds;

            // insert and encapsulate the new bounds
            history.Enqueue(bounds);
            total.Encapsulate(bounds);

            // ensure history stays within limit
            if (history.Count > limit)
            {
                // remove oldest
                history.Dequeue();

                // recalculate total bounds
                // (only needed after removing the oldest)
                total = bounds;
                foreach (Bounds b in history)
                    total.Encapsulate(b);
            }
        }

        public void Reset()
        {
            history.Clear();
            total = new Bounds();
        }
    }
}
