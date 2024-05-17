// Add this component to a Player object with collider.
// Automatically keeps a history for lag compensation.
using System;
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

    [DisallowMultipleComponent]
    [AddComponentMenu("Network/ Lag Compensation/ Lag Compensator")]
    [HelpURL("https://mirror-networking.gitbook.io/docs/manual/general/lag-compensation")]
    public class LagCompensator : NetworkBehaviour
    {
        [Header("Components")]
        [Tooltip("The collider to keep a history of.")]
        public Collider trackedCollider; // assign this in inspector

        [Header("Settings")]
        public LagCompensationSettings lagCompensationSettings = new LagCompensationSettings();
        double lastCaptureTime;

        // lag compensation history of <timestamp, capture>
        readonly Queue<KeyValuePair<double, Capture3D>> history = new Queue<KeyValuePair<double, Capture3D>>();

        [Header("Debugging")]
        public Color historyColor = Color.white;

        [ServerCallback]
        protected virtual void Update()
        {
            // capture lag compensation snapshots every interval.
            // NetworkTime.localTime because Unity 2019 doesn't have 'double' time yet.
            if (NetworkTime.localTime >= lastCaptureTime + lagCompensationSettings.captureInterval)
            {
                lastCaptureTime = NetworkTime.localTime;
                Capture();
            }
        }

        [ServerCallback]
        protected virtual void Capture()
        {
            // capture current state
            Capture3D capture = new Capture3D(
                NetworkTime.localTime,
                trackedCollider.bounds.center,
                trackedCollider.bounds.size
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

        // sampling ////////////////////////////////////////////////////////////
        // sample the sub-tick (=interpolated) history of this object for a hit test.
        // 'viewer' needs to be the player who fired!
        // for example, if A fires at B, then call B.Sample(viewer, point, tolerance).
        [ServerCallback]
        public virtual bool Sample(NetworkConnectionToClient viewer, out Capture3D sample)
        {
            // never trust the client: estimate client time instead.
            // https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking
            // the estimation is very good. the error is as low as ~6ms for the demo.
            // note that passing 'rtt' is fine: EstimateTime halves it to latency.
            double estimatedTime = LagCompensation.EstimateTime(NetworkTime.localTime, viewer.rtt, NetworkClient.bufferTime);

            // sample the history to get the nearest snapshots around 'timestamp'
            if (LagCompensation.Sample(history, estimatedTime, lagCompensationSettings.captureInterval, out Capture3D resultBefore, out Capture3D resultAfter, out double t))
            {
                // interpolate to get a decent estimation at exactly 'timestamp'
                sample = Capture3D.Interpolate(resultBefore, resultAfter, t);
                return true;
            }
            else Debug.Log($"CmdClicked: history doesn't contain {estimatedTime:F3}");

            sample = default;
            return false;
        }

        // convenience tests ///////////////////////////////////////////////////
        // there are multiple different ways to check a hit against the sample:
        // - raycasting
        // - bounds.contains
        // - increasing bounds by tolerance and checking contains
        // - threshold to bounds.closestpoint
        // let's offer a few solutions directly and see which users prefer.

        // bounds check: checks distance to closest point on bounds in history @ -rtt.
        //   'viewer' needs to be the player who fired!
        //   for example, if A fires at B, then call B.Sample(viewer, point, tolerance).
        // this is super simple and fast, but not 100% physically accurate since we don't raycast.
        [ServerCallback]
        public virtual bool BoundsCheck(
            NetworkConnectionToClient viewer,
            Vector3 hitPoint,
            float toleranceDistance,
            out float distance,
            out Vector3 nearest)
        {
            // first, sample the history at -rtt of the viewer.
            if (Sample(viewer, out Capture3D capture))
            {
                // now that we know where the other player was at that time,
                // we can see if the hit point was within tolerance of it.
                // TODO consider rotations???
                // TODO consider original collider shape??
                Bounds bounds = new Bounds(capture.position, capture.size);
                nearest = bounds.ClosestPoint(hitPoint);
                distance = Vector3.Distance(nearest, hitPoint);
                return distance <= toleranceDistance;
            }
            nearest = hitPoint;
            distance = 0;
            return false;
        }

        // raycast check: creates a collider the sampled position and raycasts to hitPoint.
        //   'viewer' needs to be the player who fired!
        //   for example, if A fires at B, then call B.Sample(viewer, point, tolerance).
        // this is physically accurate (checks against walls etc.), with the cost
        // of a runtime instantiation.
        //
        //  originPoint: where the player fired the weapon.
        //  hitPoint: where the player's local raycast hit.
        //  tolerance: scale up the sampled collider by % in order to have a bit of a tolerance.
        //             0 means no extra tolerance, 0.05 means 5% extra tolerance.
        //  layerMask: the layer mask to use for the raycast.
        [ServerCallback]
        public virtual bool RaycastCheck(
            NetworkConnectionToClient viewer,
            Vector3 originPoint,
            Vector3 hitPoint,
            float tolerancePercent,
            int layerMask,
            out RaycastHit hit)
        {
            // first, sample the history at -rtt of the viewer.
            if (Sample(viewer, out Capture3D capture))
            {
                // instantiate a real physics collider on demand.
                // TODO rotation??
                // TODO different collier types??
                GameObject temp = new GameObject("LagCompensatorTest");
                temp.transform.position = capture.position;
                BoxCollider tempCollider = temp.AddComponent<BoxCollider>();
                tempCollider.size = capture.size * (1 + tolerancePercent);

                // raycast
                Vector3 direction = hitPoint - originPoint;
                float maxDistance = direction.magnitude * 2;
                bool result = Physics.Raycast(originPoint, direction, out hit, maxDistance, layerMask);

                // cleanup
                Destroy(temp);
                return result;
            }

            hit = default;
            return false;
        }
    }
}
