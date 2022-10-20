using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public static partial class NetworkClient
    {
        // TODO expose the settings to the user later.
        // via NetMan or NetworkClientConfig or NetworkClient as component etc.

        // decrease bufferTime at runtime to see the catchup effect.
        // increase to see slowdown.
        // 'double' so we can have very precise dynamic adjustment without rounding
        [Header("Snapshot Interpolation: Buffering")]
        [Tooltip("Local simulation is behind by sendInterval * multiplier seconds.\n\nThis guarantees that we always have enough snapshots in the buffer to mitigate lags & jitter.\n\nIncrease this if the simulation isn't smooth. By default, it should be around 2.")]
        public static double bufferTimeMultiplier = 2;
        public static double bufferTime => NetworkServer.sendInterval * bufferTimeMultiplier;

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
        internal static double localTimescale = 1;

        // catchup /////////////////////////////////////////////////////////////
        // catchup thresholds in 'frames'.
        // half a frame might be too aggressive.
        [Header("Snapshot Interpolation: Catchup / Slowdown")]
        [Tooltip("Slowdown begins when the local timeline is moving too fast towards remote time. Threshold is in frames worth of snapshots.\n\nThis needs to be negative.\n\nDon't modify unless you know what you are doing.")]
        public static float catchupNegativeThreshold = -1; // careful, don't want to run out of snapshots

        [Tooltip("Catchup begins when the local timeline is moving too slow and getting too far away from remote time. Threshold is in frames worth of snapshots.\n\nThis needs to be positive.\n\nDon't modify unless you know what you are doing.")]
        public static float catchupPositiveThreshold =  1;

        [Tooltip("Local timeline acceleration in % while catching up.")]
        [Range(0, 1)]
        public static double catchupSpeed = 0.01f; // 1%

        [Tooltip("Local timeline slowdown in % while slowing down.")]
        [Range(0, 1)]
        public static double slowdownSpeed = 0.01f; // 1%

        [Tooltip("Catchup/Slowdown is adjusted over n-second exponential moving average.")]
        public static int driftEmaDuration = 1; // shouldn't need to modify this, but expose it anyway

        // we use EMA to average the last second worth of snapshot time diffs.
        // manually averaging the last second worth of values with a for loop
        // would be the same, but a moving average is faster because we only
        // ever add one value.
        static ExponentialMovingAverage driftEma;

        // dynamic buffer time adjustment //////////////////////////////////////
        // dynamically adjusts bufferTimeMultiplier for smooth results.
        // to understand how this works, try this manually:
        //
        // - disable dynamic adjustment
        // - set jitter = 0.2 (20% is a lot!)
        // - notice some stuttering
        // - disable interpolation to see just how much jitter this really is(!)
        // - enable interpolation again
        // - manually increase bufferTimeMultiplier to 3-4
        //   ... the cube slows down (blue) until it's smooth
        // - with dynamic adjustment enabled, it will set 4 automatically
        //   ... the cube slows down (blue) until it's smooth as well
        //
        // note that 20% jitter is extreme.
        // for this to be perfectly smooth, set the safety tolerance to '2'.
        // but realistically this is not necessary, and '1' is enough.
        [Header("Snapshot Interpolation: Dynamic Adjustment")]
        [Tooltip("Automatically adjust bufferTimeMultiplier for smooth results.\nSets a low multiplier on stable connections, and a high multiplier on jittery connections.")]
        public static bool dynamicAdjustment = true;

        [Tooltip("Safety buffer that is always added to the dynamic bufferTimeMultiplier adjustment.")]
        public static float dynamicAdjustmentTolerance = 1; // 1 is realistically just fine, 2 is very very safe even for 20% jitter. can be half a frame too. (see above comments)

        [Tooltip("Dynamic adjustment is computed over n-second exponential moving average standard deviation.")]
        public static int deliveryTimeEmaDuration = 2;   // 1-2s recommended to capture average delivery time
        static ExponentialMovingAverage deliveryTimeEma; // average delivery time (standard deviation gives average jitter)

        // OnValidate: see NetworkClient.cs
        // add snapshot & initialize client interpolation time if needed

        // initialization called from Awake
        static void InitTimeInterpolation()
        {
            // reset timeline & snapshots from last session (if any)
            localTimeline = 0;
            snapshots.Clear();

            // initialize EMA with 'emaDuration' seconds worth of history.
            // 1 second holds 'sendRate' worth of values.
            // multiplied by emaDuration gives n-seconds.
            driftEma        = new ExponentialMovingAverage(NetworkServer.sendRate * driftEmaDuration);
            deliveryTimeEma = new ExponentialMovingAverage(NetworkServer.sendRate * deliveryTimeEmaDuration);
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

#if !UNITY_2020_3_OR_NEWER
            // Unity 2019 doesn't have Time.timeAsDouble yet
            OnTimeSnapshot(new TimeSnapshot(connection.remoteTimeStamp, NetworkTime.localTime));
#else
            OnTimeSnapshot(new TimeSnapshot(connection.remoteTimeStamp, Time.timeAsDouble));
#endif
        }

        // see comments at the top of this file
        public static void OnTimeSnapshot(TimeSnapshot snap)
        {
            // Debug.Log($"NetworkClient: OnTimeSnapshot @ {snap.remoteTime:F3}");

            // (optional) dynamic adjustment
            if (dynamicAdjustment)
            {
                // set bufferTime on the fly.
                // shows in inspector for easier debugging :)
                bufferTimeMultiplier = SnapshotInterpolation.DynamicAdjustment(
                    NetworkServer.sendInterval,
                    deliveryTimeEma.StandardDeviation,
                    dynamicAdjustmentTolerance
                );
            }

            // insert into the buffer & initialize / adjust / catchup
            SnapshotInterpolation.InsertAndAdjust(
                snapshots,
                snap,
                ref localTimeline,
                ref localTimescale,
                NetworkServer.sendInterval,
                bufferTime,
                catchupSpeed,
                slowdownSpeed,
                ref driftEma,
                catchupNegativeThreshold,
                catchupPositiveThreshold,
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
