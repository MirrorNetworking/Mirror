// Add this component to a GameObject with collider.
// Automatically keeps a history for lag compensation.
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public struct Capture3D : Capture
    {
        public double timestamp { get; set; }
        public Vector3 position;
        public Vector3 size;

        public Capture3D(double timestamp, Vector3 position, Vector3 size)
        {
            this.timestamp = timestamp;
            this.position = position;
            this.size = size;
        }

        public void DrawGizmo()
        {
            Gizmos.DrawWireCube(position, size);
        }

        public static Capture3D Interpolate(Capture3D from, Capture3D to, double t) =>
            new Capture3D(
                0, // interpolated snapshot is applied directly. don't need timestamps.
                Vector3.LerpUnclamped(from.position, to.position, (float)t),
                Vector3.LerpUnclamped(from.size, to.size, (float)t)
            );

        public override string ToString() => $"(time={timestamp} pos={position} size={size})";
    }

    public class LagCompensator : MonoBehaviour
    {
        [Header("Components")]
        public Collider col; // assign this in inspector

        [Header("Settings")]
        public LagCompensationSettings lagCompensationSettings = new LagCompensationSettings();
        double lastCaptureTime;

        // lag compensation history of <timestamp, capture>
        Queue<KeyValuePair<double, Capture3D>> history = new Queue<KeyValuePair<double, Capture3D>>();

        [Header("Debugging")]
        public Color historyColor = Color.white;

        protected virtual void Update()
        {
            // only capture on server
            if (!NetworkServer.active) return;

            // capture lag compensation snapshots every interval.
            // NetworkTime.localTime because Unity 2019 doesn't have 'double' time yet.
            if (NetworkTime.localTime >= lastCaptureTime + lagCompensationSettings.captureInterval)
            {
                lastCaptureTime = NetworkTime.localTime;
                Capture();
            }
        }

        protected virtual void Capture()
        {
            // capture current state
            Capture3D capture = new Capture3D(
                NetworkTime.localTime,
                transform.position + col.bounds.center,
                col.bounds.size
            );

            // insert into history
            LagCompensation.Insert(history, lagCompensationSettings.historyLimit, NetworkTime.localTime, capture);
        }

        protected virtual void OnDrawGizmos()
        {
            // draw history
            Gizmos.color = historyColor;
            LagCompensation.DrawGizmos(history);
        }
    }
}
