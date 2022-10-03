using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    // empty snapshot that is only used to progress client's local timeline.
    public struct TimeSnapshot : Snapshot
    {
        public double remoteTime { get; set; }
        public double localTime { get; set; }

        public TimeSnapshot(double remoteTime, double localTime)
        {
            this.remoteTime = remoteTime;
            this.localTime = localTime;
        }
    }

    public static partial class NetworkClient
    {
        // time snapshot interpolation /////////////////////////////////////////
        // configuration settings are in .config.

        // <servertime, snaps>
        public static SortedList<double, TimeSnapshot> snapshots = new SortedList<double, TimeSnapshot>();

        // for smooth interpolation, we need to interpolate along server time.
        // any other time (arrival on client, client local time, etc.) is not
        // going to give smooth results.
        // in other words, this is the remote server's time, but adjusted.
        //
        // internal for use from NetworkTime.
        // double for long running servers, see NetworkTime comments.
        internal static double localTimeline;

        // catchup / slowdown adjustments are applied to timescale,
        // to be adjusted in every update instead of when receiving messages.
        static double localTimescale = 1;

        // we use EMA to average the last second worth of snapshot time diffs.
        // manually averaging the last second worth of values with a for loop
        // would be the same, but a moving average is faster because we only
        // ever add one value.
        static ExponentialMovingAverage driftEma;

        // average delivery time (standard deviation gives average jitter)
        static ExponentialMovingAverage deliveryTimeEma;
    }
}
