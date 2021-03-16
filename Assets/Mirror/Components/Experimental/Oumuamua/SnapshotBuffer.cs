using System.Collections.Generic;

namespace Mirror.Experimental
{
    // need to store snapshots with timestamp.
    // can't put .timestamp into snapshot because we don't want to sync it.
    internal struct BufferEntry
    {
        internal Snapshot snapshot;
        internal float timestamp;

        internal BufferEntry(Snapshot snapshot, float timestamp)
        {
            this.snapshot = snapshot;
            this.timestamp = timestamp;
        }
    }

    internal class SnapshotBuffer
    {
        Queue<BufferEntry> queue = new Queue<BufferEntry>();

        internal void Enqueue(Snapshot snapshot, float timestamp)
        {
            BufferEntry entry = new BufferEntry(snapshot, timestamp);
            queue.Enqueue(entry);
        }

        // dequeue the first snapshot if it's older enough.
        // for example, currentTime = 100, bufferInterval = 0.3
        // so any snapshot before time = 99.7
        internal bool DequeueIfOldEnough(float currentTime, float bufferInterval, out Snapshot snapshot)
        {
            if (queue.Count > 0)
            {
                // snapshot needs to be older than currentTime - bufferTime
                float thresholdTime = currentTime - bufferInterval;

                // peek and compare time
                BufferEntry entry = queue.Peek();
                if (entry.timestamp <= thresholdTime)
                {
                    snapshot = entry.snapshot;
                    queue.Dequeue();
                    return true;
                }
            }
            snapshot = default;
            return false;
        }

        // count queue size independent of time
        internal int Count => queue.Count;

        internal void Clear() => queue.Clear();
    }
}
