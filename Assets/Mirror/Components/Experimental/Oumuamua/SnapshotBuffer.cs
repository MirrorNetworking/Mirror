// the snapshot buffer's job is to hold for example 100ms worth of snapshots.
//
// from the article:
// "What we do is instead of immediately rendering snapshot data received is
//  that we buffer snapshots for a short amount of time in an interpolation
//  buffer. This interpolation buffer holds on to snapshots for a period of time
//  such that you have not only the snapshot you want to render but also,
//  statistically speaking, you are very likely to have the next snapshot as
//  well."
//
// SnapshotBuffer simply wraps a sorted list with some time functions.
// it's nothing special and we could do the time math in NetworkTransform,
// but this way we shield ourselves from life's complexities and it's testable!
using System.Collections.Generic;

namespace Mirror.Experimental
{
    public class SnapshotBuffer
    {
        // snapshots sorted by timestamp
        // in the original article, glenn fiedler drops any snapshots older than
        // the last received snapshot.
        // -> instead, we insert into a sorted buffer
        // -> the higher the buffer information density, the better
        // -> we still drop anything older than the first element in the buffer
        SortedList<float, Snapshot> list = new SortedList<float, Snapshot>();

        // insert a snapshot if it's new enough.
        // sorts it into the right position.
        public void InsertIfNewEnough(Snapshot snapshot)
        {
            // drop it if it's older than the first snapshot
            if (list.Count > 0 &&
                list.Values[0].timestamp > snapshot.timestamp)
            {
                return;
            }

            // otherwise sort it into the list
            list.Add(snapshot.timestamp, snapshot);
        }

        // dequeue the first snapshot if it's older enough.
        // for example, currentTime = 100, bufferInterval = 0.3
        // so any snapshot before time = 99.7
        public bool DequeueIfOldEnough(float currentTime, float bufferInterval, out Snapshot snapshot)
        {
            if (list.Count > 0)
            {
                // snapshot needs to be older than currentTime - bufferTime
                float thresholdTime = currentTime - bufferInterval;

                // compare time of first entry (oldest snapshot)
                Snapshot first = list.Values[0];
                if (first.timestamp <= thresholdTime)
                {
                    snapshot = first;
                    list.RemoveAt(0);
                    return true;
                }
            }
            snapshot = default;
            return false;
        }

        // peek
        public bool Peek(out Snapshot snapshot)
        {
            if (list.Count > 0)
            {
                snapshot = list.Values[0];
                return true;
            }
            snapshot = default;
            return false;
        }

        // count queue size independent of time
        public int Count => list.Count;

        // get all snapshots. useful for testing.
        public IList<Snapshot> All() => list.Values;

        public void Clear() => list.Clear();
    }
}
