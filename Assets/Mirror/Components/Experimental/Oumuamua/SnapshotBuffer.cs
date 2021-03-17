using System.Collections.Generic;

namespace Mirror.Experimental
{
    public class SnapshotBuffer
    {
        Queue<Snapshot> queue = new Queue<Snapshot>();

        public void Enqueue(Snapshot snapshot) => queue.Enqueue(snapshot);

        // dequeue the first snapshot if it's older enough.
        // for example, currentTime = 100, bufferInterval = 0.3
        // so any snapshot before time = 99.7
        public bool DequeueIfOldEnough(float currentTime, float bufferInterval, out Snapshot snapshot)
        {
            if (queue.Count > 0)
            {
                // snapshot needs to be older than currentTime - bufferTime
                float thresholdTime = currentTime - bufferInterval;

                // peek and compare time
                if (queue.Peek().timestamp <= thresholdTime)
                {
                    snapshot = queue.Dequeue();
                    return true;
                }
            }
            snapshot = default;
            return false;
        }

        // peek
        public bool Peek(out Snapshot snapshot)
        {
            if (queue.Count > 0)
            {
                snapshot = queue.Peek();
                return true;
            }
            snapshot = default;
            return false;
        }

        // count queue size independent of time
        public int Count => queue.Count;

        public void Clear() => queue.Clear();
    }
}
