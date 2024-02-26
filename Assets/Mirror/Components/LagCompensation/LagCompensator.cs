// Add this component to every Player
// Automatically keeps a history for lag compensation.
// Then create a dummy player with everything removed exept colliders.
// put in normal player position and rotation for tracked,
// then the dummy position and orientation for compensated.
// all you have to do after is call UpdateColliders() whenever a player fires.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using Mirror;

    public struct Capture3D : Capture
    {
        public double timestamp { get; set; }
        public Vector3 position;
        public Quaternion rotation;

        public Capture3D(double timestamp, Vector3 position, Quaternion rotation)
        {
            this.timestamp = timestamp;
            this.position = position;
            this.rotation = rotation;
        }

        public void DrawGizmo() { }

        public static Capture3D Interpolate(Capture3D from, Capture3D to, double t) =>
            new Capture3D(
                0, // interpolated snapshot is applied directly. don't need timestamps.
                Vector3.LerpUnclamped(from.position, to.position, (float)t),
                Quaternion.LerpUnclamped(from.rotation, to.rotation, (float)t),
            );

        public override string ToString() => $"(time={timestamp} position={position} rotation={rotation})";
    }


    public class LagCompensator : NetworkBehaviour
    {
        [Header("Components")]
        [Tooltip("The GameObject to enable / disable. NOTE: compensatedPosition & compensatedOrientation can both be compensatedGameobject. those are simply for more control.")]
        public Transform compensatedGameObject;
        [Tooltip("The Transform to Apply the tracked position")]
        public Transform compensatedPosition;
        [Tooltip("The Transform to Apply the tracked rotation.")]
        public Transform compensatedOrientation;
        [Tooltip("The position to keep track of.")]
        public Transform trackedPosition;
        [Tooltip("The rotation to keep track of.")]
        public Transform trackedOrientation;

        [Header("Settings")]
        public LagCompensationSettings lagCompensationSettings = new LagCompensationSettings();
        double lastCaptureTime;

        // lag compensation history of <timestamp, capture>
        readonly Queue<KeyValuePair<double, Capture3D>> history = new Queue<KeyValuePair<double, Capture3D>>();

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

        void Capture()
        {
            // capture current state
            Capture3D capture = new Capture3D(NetworkTime.localTime, trackedPosition.position, trackedOrientation.rotation);

            // insert into history
            LagCompensation.Insert(history, lagCompensationSettings.historyLimit, NetworkTime.localTime, capture);
        }

        protected virtual void OnDrawGizmos()
        {
            // draw history
            Gizmos.color = historyColor;
            LagCompensation.DrawGizmos(history);
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
        [Server]
        public async void UpdateColliders(NetworkConnectionToClient localCon)
        {
            // never trust the client: estimate client time instead.
            // https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking
            // the estimation is very good. the error is as low as ~6ms for the demo.
            double buffertime = (NetworkManager.singleton.sendRate / 100) * 9;
            double rtt = localCon.rtt; // the function needs rtt, which is latency * 2
            double estimatedTime = LagCompensation.EstimateTime(NetworkTime.localTime, rtt, buffertime);
            Dictionary<LagCompensator, Capture3D> resultInterp = new Dictionary<LagCompensator, Capture3D>();

            var tasks = NetworkServer.connections.Values.Select(async netcon =>
            {
                LagCompensator conPlayer = netcon.identity.GetComponent<LagCompensator>();

                // sample the history to get the nearest snapshots around 'timestamp'
                if (LagCompensation.Sample(conPlayer.history, estimatedTime, lagCompensationSettings.captureInterval, out resultBefore, out resultAfter, out double t))
                {
                    // interpolate to get a decent estimation at exactly 'timestamp'
                    resultInterp.Add(conPlayer, Capture3D.Interpolate(resultBefore, resultAfter, t));
                    resultTime = NetworkTime.localTime;
                }
                else Debug.Log($"CmdClicked: history doesn't contain {estimatedTime:F3}, netcon: {netcon}, history: {conPlayer.history.Count}");

                if (netcon == localCon)
                {
                    conPlayer.trackedGameObject.SetActive(false);
                    return;
                }
                if(!conPlayer.trackedGameObject.activeInHierarchy)
                    conPlayer.trackedGameObject.SetActive(true);

                conPlayer.compensatedPosition.position = resultInterp[conPlayer].position;
                conPlayer.compensatedOrientation.rotation = resultInterp[conPlayer].rotation;
            });
            Task.WhenAll(tasks);
        }
    }
}
