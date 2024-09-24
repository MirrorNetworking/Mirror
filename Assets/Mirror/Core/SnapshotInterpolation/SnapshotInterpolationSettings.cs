// snapshot interpolation settings struct.
// can easily be exposed in Unity inspectors.
using System;
using UnityEngine;

namespace Mirror
{
    // class so we can define defaults easily
    [Serializable]
    public class SnapshotInterpolationSettings
    {
        // decrease bufferTime at runtime to see the catchup effect.
        // increase to see slowdown.
        // 'double' so we can have very precise dynamic adjustment without rounding
        [Header("Buffering")]
        [Tooltip("Local simulation is behind by sendInterval * multiplier seconds.\n\nThis guarantees that we always have enough snapshots in the buffer to mitigate lags & jitter.\n\nIncrease this if the simulation isn't smooth. By default, it should be around 2.")]
        public double bufferTimeMultiplier = 2;

        [Tooltip("If a client can't process snapshots fast enough, don't store too many.")]
        public int bufferLimit = 32;

        // catchup /////////////////////////////////////////////////////////////
        // catchup thresholds in 'frames'.
        // half a frame might be too aggressive.
        [Header("Catchup / Slowdown")]
        [Tooltip("Slowdown begins when the local timeline is moving too fast towards remote time. Threshold is in frames worth of snapshots.\n\nThis needs to be negative.\n\nDon't modify unless you know what you are doing.")]
        public float catchupNegativeThreshold = -1; // careful, don't want to run out of snapshots

        [Tooltip("Catchup begins when the local timeline is moving too slow and getting too far away from remote time. Threshold is in frames worth of snapshots.\n\nThis needs to be positive.\n\nDon't modify unless you know what you are doing.")]
        public float catchupPositiveThreshold = 1;

        [Tooltip("Local timeline acceleration in % while catching up.")]
        [Range(0, 1)]
        public double catchupSpeed = 0.02f; // see snap interp demo. 1% is too slow.

        [Tooltip("Local timeline slowdown in % while slowing down.")]
        [Range(0, 1)]
        public double slowdownSpeed = 0.04f; // slow down a little faster so we don't encounter empty buffer (= jitter)

        [Tooltip("Catchup/Slowdown is adjusted over n-second exponential moving average.")]
        public int driftEmaDuration = 1; // shouldn't need to modify this, but expose it anyway
    }
}
