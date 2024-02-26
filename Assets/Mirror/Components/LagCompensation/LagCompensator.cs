// Add this component to every Player
// Automatically keeps a history for lag compensation.
// Then create a dummy player with everything removed exept colliders.
// put in normal player position and rotation for tracked,
// then the dummy position and orientation for compensated.
// all you have to do after is call UpdateColliders() whenever a player fires.

namespace Mirror
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Linq;
    using UnityEngine;
    using Mirror;

    public struct Capture3D : Capture
    {
        public double timestamp { get; set; }
        public Vector3 position;
        public Quaternion rotation;

        public struct animParams
        {
            public string animname;
            public int layer;
            public float time;
        }
        public animParams[] animparam;

        public Capture3D(double timestamp, Vector3 position, Quaternion rotation, animParams[] parameter)
        {
            this.timestamp = timestamp;
            this.position = position;
            this.rotation = rotation;
            this.animparam = parameter;
        }

        public void DrawGizmo() { }

        public static Capture3D Interpolate(Capture3D from, Capture3D to, double t) =>
            new Capture3D(
                0, // interpolated snapshot is applied directly. don't need timestamps.
                Vector3.LerpUnclamped(from.position, to.position, (float)t),
                Quaternion.LerpUnclamped(from.rotation, to.rotation, (float)t),
                interpAnim(from.animparam, to.animparam, t)
            );

        private static animParams[] interpAnim(animParams[] from, animParams[] to, double t)
        {
            animParams[] interped = from;

            for (int i = 0; i < interped.Length; i++)
            {
                Mathf.LerpUnclamped(interped[i].time, to[i].time, (float) t);
            }

            return interped;
        }

        public override string ToString() => $"(time={timestamp} position={position} rotation={rotation})";
    }

    [Obsolete("This is a preview version. Community feedback is welcome!")]
    public class LagCompensator : NetworkBehaviour
    {
        [Header("Config")]
        [Tooltip("Toggle to track & compensate for animation. Doesnt compensate blend tree variables. you have to manually set those on the server.")]
        public bool useAnimator;

        [Header("Components")]
        [Tooltip("The GameObject to enable / disable. NOTE: compensatedPosition & compensatedOrientation can both be compensatedGameobject. those are simply for more control.")]
        public GameObject compensatedGameObject;
        [Tooltip("The Transform to Apply the tracked position")]
        public Transform compensatedPosition;
        [Tooltip("The Transform to Apply the tracked rotation.")]
        public Transform compensatedOrientation;
        [Tooltip("Only needed if useAnimator is enabled")]
        public Animator compensatedAnimator;
        [Tooltip("The position to keep track of.")]
        public Transform trackedPosition;
        [Tooltip("The rotation to keep track of.")]
        public Transform trackedOrientation;
        [Tooltip("Only needed if useAnimator is enabled")]
        public Animator trackedAnimator;

        [Header("Settings")]
        public LagCompensationSettings lagCompensationSettings = new LagCompensationSettings();
        double lastCaptureTime;

        // lag compensation history of <timestamp, capture>
        readonly Queue<KeyValuePair<double, Capture3D>> history = new Queue<KeyValuePair<double, Capture3D>>();

        [Header("Debugging")]
        public Color historyColor = Color.white;
        public double resultDuration = 0.5;
        double resultTime;
        Capture3D resultBefore;
        Capture3D resultAfter;
        Capture3D resultInterpolated;


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
            if (useAnimator)
            {
                Capture3D.animParams[] animParamsArray = new Capture3D.animParams[trackedAnimator.layerCount];
                for (int i = 0; i < trackedAnimator.layerCount; i++)
                {
                    animParamsArray[i].layer = i;
                    animParamsArray[i].animname = trackedAnimator.GetCurrentAnimatorClipInfo(i)[0].clip.name;
                    animParamsArray[i].time = trackedAnimator.GetCurrentAnimatorStateInfo(i).normalizedTime;
                }

                Capture3D capture = new Capture3D(NetworkTime.localTime, trackedPosition.position, trackedOrientation.rotation, animParamsArray);

                // insert into history
                LagCompensation.Insert(history, lagCompensationSettings.historyLimit, NetworkTime.localTime, capture);
            }
            else
            {
                // capture current state
                Capture3D capture = new Capture3D(NetworkTime.localTime, trackedPosition.position, trackedOrientation.rotation, Array.Empty<Capture3D.animParams>());

                // insert into history
                LagCompensation.Insert(history, lagCompensationSettings.historyLimit, NetworkTime.localTime, capture);
            }
        }

        protected virtual void OnDrawGizmos()
        {
            // draw history
            Gizmos.color = historyColor;
            LagCompensation.DrawGizmos(history);
        }

        // Updates every players compensated colliders with the Callers estimated time.
        [Server]
        public async void UpdateColliders(NetworkConnectionToClient localCon = null)
        {
            // never trust the client: estimate client time instead.
            double buffertime = (NetworkManager.singleton.sendRate / 100) * 9;
            double rtt = localCon.rtt; // the function needs rtt, which is latency * 2
            double estimatedTime = LagCompensation.EstimateTime(NetworkTime.localTime, rtt, buffertime);

			// Honestly, couldnt tell you why this is a dictionary.
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
                    conPlayer.compensatedGameObject.SetActive(false);
                    return;
                }
                if(!conPlayer.compensatedGameObject.activeInHierarchy)
                    conPlayer.compensatedGameObject.SetActive(true);

                conPlayer.compensatedPosition.position = resultInterp[conPlayer].position;
                conPlayer.compensatedOrientation.rotation = resultInterp[conPlayer].rotation;

                // Animation Compensation
                if (useAnimator)
                {
                    for (int i = 0; i < resultInterp[conPlayer].animparam.Length; i++)
                    {
                        conPlayer.compensatedAnimator.Play(resultInterp[conPlayer].animparam[i].animname, resultInterp[conPlayer].animparam[i].layer, resultInterp[conPlayer].animparam[i].time);
                    }
                    // NOTE: Doesnt set the variables of BLEND TREES. you will have to set the blend tree variables manually on the server.
                    // OR add them to Capture3d and interpolate. this HAS to be done manually.
                }
            });
            await Task.WhenAll(tasks);
        }
    }
}
