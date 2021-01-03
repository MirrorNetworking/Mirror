using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror.TransformSyncing
{
    public class SnapshotBuffer
    {
        static readonly ILogger logger = LogFactory.GetLogger<SnapshotBuffer>(LogType.Error);
        struct Snapshot
        {
            /// <summary>
            /// Server Time
            /// </summary>
            public readonly float time;
            public readonly TransformState state;

            public Snapshot(TransformState state, float time) : this()
            {
                this.state = state;
                this.time = time;
            }
        }

        readonly List<Snapshot> buffer = new List<Snapshot>();

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => buffer.Count == 0;
        }

        public void AddSnapShot(TransformState state, float serverTime)
        {
            buffer.Add(new Snapshot(state, serverTime));
        }

        /// <summary>
        /// Gets snapshot to use for interoplation
        /// <para>this method should not be called when there are no snapshots in buffer</para>
        /// </summary>
        /// <param name="now"></param>
        /// <returns></returns>
        public TransformState GetLinearInterpolation(float now)
        {
            if (buffer.Count == 0)
            {
                logger.LogWarning("No snapshots, returning default");
                return default;
            }

            // first snapshot
            if (buffer.Count == 1)
            {
                Snapshot only = buffer[0];
                return only.state;
            }

            for (int i = 0; i < buffer.Count - 1; i++)
            {
                Snapshot from = buffer[i];
                Snapshot to = buffer[i + 1];
                float fromTime = buffer[i].time;
                float toTime = buffer[i + 1].time;

                // if between times, then use from/to
                if (fromTime > now && toTime < now)
                {
                    float alpha = Mathf.Clamp01((now - fromTime) / (toTime - fromTime));

                    Vector3 pos = Vector3.Lerp(from.state.position, to.state.position, alpha);
                    Quaternion rot = Quaternion.Slerp(from.state.rotation, to.state.rotation, alpha);
                    return new TransformState(pos, rot);
                }
            }

            // if no valid snapshot use last
            // this can happen if server hasn't sent new data
            // there could be no new data from either lag or because object hasn't moved
            Snapshot last = buffer[buffer.Count - 1];
            return last.state;
        }

        /// <summary>
        /// removes snapshots older than <paramref name="oldTime"/>, but keeps atleast <paramref name="keepCount"/> snapshots in buffer
        /// </summary>
        /// <param name="oldTime"></param>
        /// <param name="keepCount">minium number of snapshots to keep in buffer</param>
        public void RemoveOldSnapshots(float oldTime, int keepCount)
        {
            for (int i = buffer.Count - 1 - keepCount; i >= 0; i++)
            {
                // older than oldTime
                if (buffer[i].time < oldTime)
                {
                    buffer.RemoveAt(i);
                }
            }
        }
    }
}
