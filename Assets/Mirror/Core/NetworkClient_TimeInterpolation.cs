using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public static partial class NetworkClient
    {
        // snapshot interpolation settings /////////////////////////////////////
        // TODO expose the settings to the user later.
        // via NetMan or NetworkClientConfig or NetworkClient as component etc.
        public static SnapshotInterpolationSettings snapshotSettings = new SnapshotInterpolationSettings();

        // snapshot interpolation runtime data /////////////////////////////////
        // buffer time is dynamically adjusted.
        // store the current multiplier here, without touching the original in settings.
        // this way we can easily reset to or compare with original where needed.
        public static double bufferTimeMultiplier;

        // original buffer time based on the settings
        // dynamically adjusted buffer time based on dynamically adjusted multiplier
        public static double initialBufferTime => NetworkServer.sendInterval * snapshotSettings.bufferTimeMultiplier;
        public static double bufferTime        => NetworkServer.sendInterval * bufferTimeMultiplier;

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
            // reset timeline, localTimescale & snapshots from last session (if any)
            bufferTimeMultiplier = snapshotSettings.bufferTimeMultiplier;
            localTimeline = 0;
            localTimescale = 1;
            snapshots.Clear();

            // initialize EMA with 'emaDuration' seconds worth of history.
            // 1 second holds 'sendRate' worth of values.
            // multiplied by emaDuration gives n-seconds.
            driftEma = new ExponentialMovingAverage(NetworkServer.sendRate * snapshotSettings.driftEmaDuration);
            deliveryTimeEma = new ExponentialMovingAverage(NetworkServer.sendRate * snapshotSettings.deliveryTimeEmaDuration);
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

            // Unity 2019 doesn't have Time.timeAsDouble yet
            OnTimeSnapshot(new TimeSnapshot(connection.remoteTimeStamp, NetworkTime.localTime));
        }

        // see comments at the top of this file
        public static void OnTimeSnapshot(TimeSnapshot snap)
        {
            // Debug.Log($"NetworkClient: OnTimeSnapshot @ {snap.remoteTime:F3}");

            // (optional) dynamic adjustment
            if (snapshotSettings.dynamicAdjustment)
            {
                // set bufferTime on the fly.
                // shows in inspector for easier debugging :)
                bufferTimeMultiplier = SnapshotInterpolation.DynamicAdjustment(
                    NetworkServer.sendInterval,
                    deliveryTimeEma.StandardDeviation,
                    snapshotSettings.dynamicAdjustmentTolerance
                );
            }

            // insert into the buffer & initialize / adjust / catchup
            SnapshotInterpolation.InsertAndAdjust(
                snapshots,
                snapshotSettings.bufferLimit,
                snap,
                ref localTimeline,
                ref localTimescale,
                NetworkServer.sendInterval,
                bufferTime,
                snapshotSettings.catchupSpeed,
                snapshotSettings.slowdownSpeed,
                ref driftEma,
                snapshotSettings.catchupNegativeThreshold,
                snapshotSettings.catchupPositiveThreshold,
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
                // NetworkTime uses unscaled time and ignores Time.timeScale.
                // fixes Time.timeScale getting server & client time out of sync:
                // https://github.com/MirrorNetworking/Mirror/issues/3409
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
