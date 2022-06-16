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

        [Tooltip("Start to accelerate interpolation if buffer size is >= threshold. Needs to be larger than bufferTimeMultiplier.")]
        public int catchupThreshold = 4;

        [Tooltip("Once buffer is larger catchupThreshold, accelerate by multiplier % per excess entry.")]
        [Range(0, 1)] public float catchupMultiplier = 0.10f;

        [Header("Buffering")]
        [Tooltip("Snapshots are buffered for sendInterval * multiplier seconds. If your expected client base is to run at non-ideal connection quality (2-5% packet loss), 3x supposedly works best.")]
        public int bufferTimeMultiplier = 1;
        public float bufferTime => server.sendInterval * bufferTimeMultiplier;

        [Header("Debug")]
        public Color catchupColor = Color.green;
        Color defaultColor;

        // absolute interpolation time, moved along with deltaTime
        // (roughly between [0, delta] where delta is snapshot B - A timestamp)
        // (can be bigger than delta when overshooting)
        double serverInterpolationTime;

        // for debugging
        double lastCatchup;

        // <servertime, snaps>
        public SortedList<double, Snapshot3D> snapshots = new SortedList<double, Snapshot3D>();
        Func<Snapshot3D, Snapshot3D, double, Snapshot3D> Interpolate = Snapshot3D.Interpolate;

        void Awake()
        {
            defaultColor = render.sharedMaterial.color;
        }

        public void OnMessage(Snapshot3D snap)
        {
            snap.localTimestamp = Time.time;
            SnapshotInterpolation.InsertIfNewEnough(snap, snapshots);
        }

        void Update()
        {
            // snapshot interpolation
            if (interpolate)
            {
                // compute snapshot interpolation & apply if any was spit out
                // TODO we don't have Time.deltaTime double yet. float is fine.
                if (SnapshotInterpolation.Compute(
                    Time.time,
                    Time.deltaTime,
                    ref serverInterpolationTime,
                    bufferTime,
                    snapshots,
                    catchupThreshold, catchupMultiplier,
                    Interpolate,
                    out Snapshot3D computed,
                    out lastCatchup))
                {
                    transform.position = computed.position;
                }
            }
            // apply raw
            else
            {
                if (snapshots.Count > 0)
                {
                    Snapshot3D snap = snapshots.Values[0];
                    transform.position = snap.position;
                    snapshots.RemoveAt(0);
                }
            }

            // color material while catching up
            render.material.color = lastCatchup > 0
                ? catchupColor
                : defaultColor;
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
    }
}
