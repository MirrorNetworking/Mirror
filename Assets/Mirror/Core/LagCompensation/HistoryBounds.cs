// HistoryBounds keeps a bounding box of all the object's bounds in the past N seconds.
// useful to decide which objects to rollback, instead of rolling back all of them.
// https://www.youtube.com/watch?v=zrIY0eIyqmI (37:00)
// standalone C# implementation to be engine (and language) agnostic.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror
{
    // by default, SortedList is sorted from smallest to largest.
    // for max coordinates, we need to sort from largest to smallest.
    class DescendingComparer : IComparer<float>
    {
        public int Compare(float a, float b) =>
            -Comparer<float>.Default.Compare(a, b);
    }

    public class HistoryBounds
    {
        // history of bounds
        // readonly Queue<Bounds> history;
        // public int Count => history.Count;

        // instead of keeping a history of Queue<Bounds> for each timestamp,
        // we split it into xmin, xmax, ymin, ymax, ... sorted lists.
        // each entry has (value, timestamp).
        // this way when removing, we can easily walk the sorted list.
        //
        // for example:
        //  xmin: (1, t0), (3, t2), (4, t1)
        //  when it's time to remove oldest=t0, we remove (1, t0)
        //
        //  xmin: (3, t2), (4, t1)
        //  when it's time to remove oldest=t1, we check (3, t2) which is too new.
        //  we remove nothing
        //
        //  when it's time to remove oldest=t2, we check (3, t2) which is ready to be removed.
        //  we also check the next one: (4, t1) which is also older.
        //  we keep checking until one is newer.
        //
        // for total bounds, we always use xmin.peek(), xmax.peek(), etc.
        readonly SortedList<float, int> xmin, xmax;
        readonly SortedList<float, int> ymin, ymax;
        readonly SortedList<float, int> zmin, zmax;

        // current insertion timestamp.
        // TODO what if overflows?
        int timestamp = 0;

        // keep a custom count since lists may be larger than actual count while
        // we're still waiting for the oldest to be ready to be removed.
        public int Count { get; private set; }

        // history limit. oldest bounds will be removed.
        public readonly int limit;

        // only remove old entries every n-th insertion.
        // new entries are still encapsulated on every insertion.
        // for example, every 2nd insertion is enough, and 2x as fast.
        public readonly int recalculateEveryNth;
        int recalculateCounter = 0;

        // total bounds encapsulating all of the bounds history
        public Bounds total;

        public HistoryBounds(int limit, int recalculateEveryNth)
        {
            // initialize queue with maximum capacity to avoid runtime resizing
            // +1 because it makes the code easier if we insert first, and then remove.
            this.limit = limit;
            this.recalculateEveryNth = recalculateEveryNth;
            xmin = new SortedList<float, int>(limit + 1);
            ymin = new SortedList<float, int>(limit + 1);
            zmin = new SortedList<float, int>(limit + 1);
            xmax = new SortedList<float, int>(limit + 1, new DescendingComparer());
            ymax = new SortedList<float, int>(limit + 1, new DescendingComparer());
            zmax = new SortedList<float, int>(limit + 1, new DescendingComparer());
        }

        // enqueue a value into a sorted list.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void AddOrReplace(SortedList<float, int> list, float value, int timestamp)
        {
            // players may revist the same coordinates multiple times.
            // inserting the same key twice would throw.
            // instead, overwrite the key's value timestamp with the newer one.
            if (list.ContainsKey(value))
            {
                list[value] = timestamp;
            }
            else
            {
                list.Add(value, timestamp);
            }
        }

        // split bounds and add to sorted lists
        void Enqueue(Bounds bounds)
        {
            // insert individual coordinates into the sorted lists
            AddOrReplace(xmin, bounds.min.x, timestamp);
            AddOrReplace(ymin, bounds.min.y, timestamp);
            AddOrReplace(zmin, bounds.min.z, timestamp);
            AddOrReplace(xmax, bounds.max.x, timestamp);
            AddOrReplace(ymax, bounds.max.y, timestamp);
            AddOrReplace(zmax, bounds.max.z, timestamp);

            // timestamp always increases, never decreases.
            timestamp += 1;

            // keep count of intended amount of entries.
            // decreases when dequeueing.
            Count += 1;
        }

        // walk the sorted lists to remove all old entries up to timestamp.
        // the lists are sorted by value, not by timestamp:
        // (2, t0), (3, t2), (4, t1), ...
        // sometimes there's nothing to remove.
        // sometimes there are one more values to remove.
        static void RemoveOldest(SortedList<float, int> list, int old)
        {
            // find out until which index we need to remove.
            for (int i = 0; i < list.Count; ++i)
            {
                // get the timestamp at [index]
                int timestamp = list.Values[i];

                // is this older than the timestamp limit?
                if (timestamp < old)
                {
                    list.RemoveAt(i);
                    --i;
                }
                // otherwise stop for now
                else break;
            }
        }

        // walk the sorted lists to remove all old entries up to timestamp
        void RemoveOldest()
        {
            // calculate the target timestamp to remove up to.
            // we are now at 'timestamp'.
            // we allow 'limit' entries.
            // so we want to remove everything older than 'timestamp - limit'.
            int old = timestamp - limit;

            // remove oldest in ascending order from min lists
            RemoveOldest(xmin, old);
            RemoveOldest(ymin, old);
            RemoveOldest(zmin, old);

            // remove oldest in descending order from max lists
            RemoveOldest(xmax, old);
            RemoveOldest(ymax, old);
            RemoveOldest(zmax, old);

            // even though not all list's counts may change just yet,
            // we still decrease the true count.
            Count -= 1;
        }

        // build total bounds from the first of each sorted list
        void RecalculateTotal()
        {
            Vector3 min = new Vector3(xmin.Keys[0], ymin.Keys[0], zmin.Keys[0]);
            Vector3 max = new Vector3(xmax.Keys[0], ymax.Keys[0], zmax.Keys[0]);
            total.SetMinMax(min, max);
        }

        // insert new bounds into history. calculates new total bounds.
        // Queue.Dequeue() always has the oldest bounds.
        public void Insert(Bounds bounds)
        {
            // initialize 'total' if not initialized yet.
            // we don't want to call (0,0).Encapsulate(bounds).
            if (Count == 0)
                total = bounds;

            // insert and encapsulate the new bounds
            Enqueue(bounds);
            total.Encapsulate(bounds);

            // ensure history stays within limit
            if (Count > limit)
            {
                // remove oldest
                RemoveOldest();

                // optimization: only recalculate every n-th removal.
                // accurate enough, and N times faster.
                if (++recalculateCounter < recalculateEveryNth)
                    return;

                // reset counter
                recalculateCounter = 0;

                // recalculate total bounds
                // (only needed after removing the oldest)
                RecalculateTotal();
            }
        }

        public void Reset()
        {
            xmin.Clear();
            xmax.Clear();
            ymin.Clear();
            ymax.Clear();
            zmin.Clear();
            zmax.Clear();
            Count = 0;
            timestamp = 0;
            total = new Bounds();
        }
    }
}
