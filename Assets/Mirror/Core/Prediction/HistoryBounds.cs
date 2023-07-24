// HistoryBounds keeps a bounding box of all the object's bounds in the past N seconds.
// useful to decide which objects we should rollback & raycast against.
// https://www.youtube.com/watch?v=zrIY0eIyqmI (37:00)
// standalone C# implementation to be engine (and language) agnostic.

using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public static class HistoryBounds
    {
        // insert current bounds into history. returns new total bounds.
        public static Bounds Insert(Queue<Bounds> history, Bounds bounds)
        {
            // TODO insert new
            // TODO remove old based on history limit

            return bounds;
        }


        // TODO .bounds that wraps past N bounds
        // TODO fast data structure to always .encapsulate latest and .remove oldest

        // TODO update:
        // - capture bounds every few seconds
        // - build new totalBounds only when capturing, not every .totalBounds call

        // TODO runtime debug gizmo
    }
}
