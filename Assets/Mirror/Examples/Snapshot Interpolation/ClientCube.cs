using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Examples.SnapshotInterpolationDemo
{
    public class ClientCube : MonoBehaviour
    {
        [Header("Components")]
        public ServerCube server;
        public Renderer render;

        [Header("Toggle")]
        public bool interpolate = true;

        // decrease bufferTime at runtime to see the catchup effect.
        // increase to see slowdown.
        // 'double' so we can have very precise dynamic adjustment without rounding
        [Header("Buffering")]
        [Tooltip("Local simulation is behind by sendInterval * multiplier seconds.\n\nThis guarantees that we always have enough snapshots in the buffer to mitigate lags & jitter.\n\nIncrease this if the simulation isn't smooth. By default, it should be around 2.")]
        public double bufferTimeMultiplier = 2;
        public double bufferTime => server.sendInterval * bufferTimeMultiplier;

        // <servertime, snaps>
        public SortedList<double, Snapshot3D> snapshots = new SortedList<double, Snapshot3D>();

        // for smooth interpolation, we need to interpolate along server time.
        // any other time (arrival on client, client local time, etc.) is not
        // going to give smooth results.
        double localTimeline;

        // catchup / slowdown adjustments are applied to timescale,
        // to be adjusted in every update instead of when receiving messages.
        double localTimescale = 1;

        // catchup /////////////////////////////////////////////////////////////
        // catchup thresholds in 'frames'.
        // half a frame might be too aggressive.
        [Header("Catchup / Slowdown")]
        [Tooltip("Slowdown begins when the local timeline is moving too fast towards remote time. Threshold is in frames worth of snapshots.\n\nThis needs to be negative.\n\nDon't modify unless you know what you are doing.")]
        public float catchupNegativeThreshold = -1; // careful, don't want to run out of snapshots

        [Tooltip("Catchup begins when the local timeline is moving too slow and getting too far away from remote time. Threshold is in frames worth of snapshots.\n\nThis needs to be positive.\n\nDon't modify unless you know what you are doing.")]
        public float catchupPositiveThreshold =  1;

        [Tooltip("Local timeline acceleration in % while catching up.")]
        [Range(0, 1)]
        public double catchupSpeed = 0.01f; // 1%

        [Tooltip("Local timeline slowdown in % while slowing down.")]
        [Range(0, 1)]
        public double slowdownSpeed = 0.01f; // 1%

        [Tooltip("Catchup/Slowdown is adjusted over n-second exponential moving average.")]
        public int driftEmaDuration = 1; // shouldn't need to modify this, but expose it anyway

        // we use EMA to average the last second worth of snapshot time diffs.
        // manually averaging the last second worth of values with a for loop
        // would be the same, but a moving average is faster because we only
        // ever add one value.
        ExponentialMovingAverage driftEma;

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
        [Header("Dynamic Adjustment")]
        [Tooltip("Automatically adjust bufferTimeMultiplier for smooth results.\nSets a low multiplier on stable connections, and a high multiplier on jittery connections.")]
        public bool dynamicAdjustment = true;

        [Tooltip("Safety buffer that is always added to the dynamic bufferTimeMultiplier adjustment.")]
        public float dynamicAdjustmentTolerance = 1; // 1 is realistically just fine, 2 is very very safe even for 20% jitter. can be half a frame too. (see above comments)

        [Tooltip("Dynamic adjustment is computed over n-second exponential moving average standard deviation.")]
        public int deliveryTimeEmaDuration = 2;   // 1-2s recommended to capture average delivery time
        ExponentialMovingAverage deliveryTimeEma; // average delivery time (standard deviation gives average jitter)

        // debugging ///////////////////////////////////////////////////////////
        [Header("Debug")]
        public Color catchupColor = Color.green; // green traffic light = go fast
        public Color slowdownColor = Color.red;  // red traffic light = go slow
        Color        defaultColor;

        void Awake()
        {
            defaultColor = render.sharedMaterial.color;

            // initialize EMA with 'emaDuration' seconds worth of history.
            // 1 second holds 'sendRate' worth of values.
            // multiplied by emaDuration gives n-seconds.
            driftEma = new ExponentialMovingAverage(server.sendRate * driftEmaDuration);
            deliveryTimeEma = new ExponentialMovingAverage(server.sendRate * deliveryTimeEmaDuration);
        }

        // add snapshot & initialize client interpolation time if needed
        public void OnMessage(Snapshot3D snap)
        {
            // set local timestamp (= when it was received on our end)
#if !UNITY_2020_3_OR_NEWER
            snap.localTime = NetworkTime.localTime;
#else
            snap.localTime = Time.timeAsDouble;
#endif

            // (optional) dynamic adjustment
            if (dynamicAdjustment)
            {
                // set bufferTime on the fly.
                // shows in inspector for easier debugging :)
                bufferTimeMultiplier = SnapshotInterpolation.DynamicAdjustment(
                    server.sendInterval,
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
                server.sendInterval,
                bufferTime,
                catchupSpeed,
                slowdownSpeed,
                ref driftEma,
                catchupNegativeThreshold,
                catchupPositiveThreshold,
                ref deliveryTimeEma);
        }

        void Update()
        {
            // only while we have snapshots.
            // timeline starts when the first snapshot arrives.
            if (snapshots.Count > 0)
            {
                // snapshot interpolation
                if (interpolate)
                {
                    // step
                    SnapshotInterpolation.Step(
                        snapshots,
                        Time.unscaledDeltaTime,
                        ref localTimeline,
                        localTimescale,
                        out Snapshot3D fromSnapshot,
                        out Snapshot3D toSnapshot,
                        out double t);

                    // interpolate & apply
                    Snapshot3D computed = Snapshot3D.Interpolate(fromSnapshot, toSnapshot, t);
                    transform.position = computed.position;
                }
                // apply raw
                else
                {
                    Snapshot3D snap = snapshots.Values[0];
                    transform.position = snap.position;
                    snapshots.RemoveAt(0);
                }
            }

            // color material while catching up / slowing down
            if (localTimescale < 1)
                render.material.color = slowdownColor;
            else if (localTimescale > 1)
                render.material.color = catchupColor;
            else
                render.material.color = defaultColor;
        }

        void OnGUI()
        {
            // display buffer size as number for easier debugging.
            // catchup is displayed as color state in Update() already.
            const int width = 30; // fit 3 digits
            const int height = 20;
            Vector2 screen = Camera.main.WorldToScreenPoint(transform.position);
            string str = $"{snapshots.Count}";
            GUI.Label(new Rect(screen.x - width / 2, screen.y - height / 2, width, height), str);
        }

        void OnValidate()
        {
            // thresholds need to be <0 and >0
            catchupNegativeThreshold = Math.Min(catchupNegativeThreshold, 0);
            catchupPositiveThreshold = Math.Max(catchupPositiveThreshold, 0);
        }
    }
}
