// HistoryBounds keeps a bounding box of all the object's bounds in the past N seconds.
// useful to decide which objects to rollback, instead of rolling back all of them.
// https://www.youtube.com/watch?v=zrIY0eIyqmI (37:00)
// standalone C# implementation to be engine (and language) agnostic.

using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public static class HistoryBounds
    {
        // insert current bounds into history. returns new total bounds.
        // Queue.Dequeue() always has the oldest bounds.
        public static Bounds Insert(Queue<Bounds> history, int limit, Bounds bounds)
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
            Bounds total = bounds;
            foreach (Bounds b in history)
                total.Encapsulate(b);

            return total;
        }


        // TODO .bounds that wraps past N bounds
        // TODO fast data structure to always .encapsulate latest and .remove oldest

        // TODO update:
        // - capture bounds every few seconds
        // - build new totalBounds only when capturing, not every .totalBounds call

        // TODO runtime debug gizmo
    }
}
