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
        public static SortedList<double, TimeSnapshot> snapshots =
            new SortedList<double, TimeSnapshot>();

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

        // OnValidate: see NetworkClient.cs
        // add snapshot & initialize client interpolation time if needed

        // initialization called from Awake
        static void InitTimeInterpolation()
        {
            // initialize EMA with 'emaDuration' seconds worth of history.
            // 1 second holds 'sendRate' worth of values.
            // multiplied by emaDuration gives n-seconds.
            driftEma        = new ExponentialMovingAverage(NetworkServer.config.sendRate * config.driftEmaDuration);
            deliveryTimeEma = new ExponentialMovingAverage(NetworkServer.config.sendRate * config.deliveryTimeEmaDuration);
        }

        // server sends TimeSnapshotMessage every sendInterval.
        // batching already includes the remoteTimestamp.
        // we simply insert it on-message here.
        // => only for reliable channel. unreliable would always arrive earlier.
        static void OnTimeSnapshotMessage(TimeSnapshotMessage _)
        {
            // insert another snapshot for snapshot interpolation.
            // before calling OnDeserialize so components can use
            // NetworkTime.time and NetworkTime.timeStamp.
            OnTimeSnapshot(new TimeSnapshot(connection.remoteTimeStamp, Time.timeAsDouble));
        }

        // see comments at the top of this file
        public static void OnTimeSnapshot(TimeSnapshot snap)
        {
            // Debug.Log($"NetworkClient: OnTimeSnapshot @ {snap.remoteTime:F3}");

            // (optional) dynamic adjustment
            if (config.dynamicAdjustment)
            {
                // set bufferTime on the fly.
                // shows in inspector for easier debugging :)
                config.bufferTimeMultiplier = SnapshotInterpolation.DynamicAdjustment(
                    NetworkServer.config.sendInterval,
                    deliveryTimeEma.StandardDeviation,
                    config.dynamicAdjustmentTolerance
                );
            }

            // insert into the buffer & initialize / adjust / catchup
            SnapshotInterpolation.InsertAndAdjust(
                snapshots,
                snap,
                ref localTimeline,
                ref localTimescale,
                NetworkServer.config.sendInterval,
                config.bufferTime,
                config.catchupSpeed,
                config.slowdownSpeed,
                ref driftEma,
                config.catchupNegativeThreshold,
                config.catchupPositiveThreshold,
                ref deliveryTimeEma);

            // Debug.Log($"inserted TimeSnapshot remote={snap.remoteTime:F2} local={snap.localTime:F2} total={snapshots.Count}");
        }

        // call this from early update, so the timeline is safe to use in update
        static void UpdateTimeInterpolation()
        {
            // only while we have snapshots.
            // timeline starts when the first snapshot arrives.
            if (snapshots.Count > 0)
            {
                // progress local timeline.
                SnapshotInterpolation.StepTime(Time.unscaledDeltaTime, ref localTimeline, localTimescale);

                // progress local interpolation.
                // TimeSnapshot doesn't interpolate anything.
                // this is merely to keep removing older snapshots.
                SnapshotInterpolation.StepInterpolation(snapshots, localTimeline, out _, out _, out double t);
                // Debug.Log($"NetworkClient SnapshotInterpolation @ {localTimeline:F2} t={t:F2}");
            }
        }
    }
}
