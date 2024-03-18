using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace Mirror.Examples.LagCompensationDemo
{
    public class ServerCube : MonoBehaviour
    {
        [Header("Components")]
        public ClientCube client;
        [FormerlySerializedAs("collider")]
        public BoxCollider col;

        [Header("Movement")]
        public float distance = 10;
        public float speed = 3;
        Vector3 start;

        [Header("Snapshot Interpolation")]
        [Tooltip("Send N snapshots per second. Multiples of frame rate make sense.")]
        public int sendRate = 30; // in Hz. easier to work with as int for EMA. easier to display '30' than '0.333333333'
        public float sendInterval => 1f / sendRate;
        float lastSendTime;

        [Header("Lag Compensation")]
        public LagCompensationSettings lagCompensationSettings = new LagCompensationSettings();
        double lastCaptureTime;

        // lag compensation history of <timestamp, capture>
        Queue<KeyValuePair<double, Capture2D>> history = new Queue<KeyValuePair<double, Capture2D>>();

        public Color historyColor = Color.white;

        // store latest lag compensation result to show a visual indicator
        [Header("Debug")]
        public double resultDuration = 0.5;
        double resultTime;
        Capture2D resultBefore;
        Capture2D resultAfter;
        Capture2D resultInterpolated;

        [Header("Latency Simulation")]
        [Tooltip("Latency in seconds")]
        public float latency = 0.05f; // 50 ms
        [Tooltip("Latency jitter, randomly added to latency.")]
        [Range(0, 1)] public float jitter = 0.05f;
        [Tooltip("Packet loss in %")]
        [Range(0, 1)] public float loss = 0.1f;
        [Tooltip("Scramble % of unreliable messages, just like over the real network. Mirror unreliable is unordered.")]
        [Range(0, 1)] public float scramble = 0.1f;

        // random
        // UnityEngine.Random.value is [0, 1] with both upper and lower bounds inclusive
        // but we need the upper bound to be exclusive, so using System.Random instead.
        // => NextDouble() is NEVER < 0 so loss=0 never drops!
        // => NextDouble() is ALWAYS < 1 so loss=1 always drops!
        System.Random random = new System.Random();

        // hold on to snapshots for a little while before delivering
        // <deliveryTime, snapshot>
        List<(double, Snapshot3D)> queue = new List<(double, Snapshot3D)>();

        // latency simulation:
        // always a fixed value + some jitter.
        float SimulateLatency() => latency + Random.value * jitter;

        // this is the average without randomness. for lag compensation math.
        // in a real game, use rtt instead.
        float AverageLatency() => latency + 0.5f * jitter;

        void Start()
        {
            start = transform.position;
        }

        void Update()
        {
            // move on XY plane
            float x = Mathf.PingPong(Time.time * speed, distance);
            transform.position = new Vector3(start.x + x, start.y, start.z);

            // broadcast snapshots every interval
            if (Time.time >= lastSendTime + sendInterval)
            {
                Send(transform.position);
                lastSendTime = Time.time;
            }

            Flush();

            // capture lag compensation snapshots every interval.
            // NetworkTime.localTime because Unity 2019 doesn't have 'double' time yet.
            if (NetworkTime.localTime >= lastCaptureTime + lagCompensationSettings.captureInterval)
            {
                lastCaptureTime = NetworkTime.localTime;
                Capture();
            }
        }

        void Send(Vector3 position)
        {
            // create snapshot
            // Unity 2019 doesn't have Time.timeAsDouble yet
            Snapshot3D snap = new Snapshot3D(NetworkTime.localTime, 0, position);

            // simulate packet loss
            bool drop = random.NextDouble() < loss;
            if (!drop)
            {
                // simulate scramble (Random.Next is < max, so +1)
                bool doScramble = random.NextDouble() < scramble;
                int last = queue.Count;
                int index = doScramble ? random.Next(0, last + 1) : last;

                // simulate latency
                float simulatedLatency = SimulateLatency();
                // Unity 2019 doesn't have Time.timeAsDouble yet
                double deliveryTime = NetworkTime.localTime + simulatedLatency;
                queue.Insert(index, (deliveryTime, snap));
            }
        }

        void Flush()
        {
            // flush ready snapshots to client
            for (int i = 0; i < queue.Count; ++i)
            {
                (double deliveryTime, Snapshot3D snap) = queue[i];

                // Unity 2019 doesn't have Time.timeAsDouble yet
                if (NetworkTime.localTime >= deliveryTime)
                {
                    client.OnMessage(snap);
                    queue.RemoveAt(i);
                    --i;
                }
            }
        }

        void Capture()
        {
            // capture current state
            Capture2D capture = new Capture2D(NetworkTime.localTime, transform.position, col.size);

            // insert into history
            LagCompensation.Insert(history, lagCompensationSettings.historyLimit, NetworkTime.localTime, capture);
        }

        // client says: "I was clicked here, at this time."
        // server needs to rollback to validate.
        // timestamp is the client's snapshot interpolated timeline!
        public bool CmdClicked(Vector2 position)
        {
            // never trust the client: estimate client time instead.
            // https://developer.valvesoftware.com/wiki/Source_Multiplayer_Networking
            // the estimation is very good. the error is as low as ~6ms for the demo.
            double rtt = AverageLatency() * 2; // the function needs rtt, which is latency * 2
            double estimatedTime = LagCompensation.EstimateTime(NetworkTime.localTime, rtt, client.bufferTime);

            // compare estimated time with actual client time for debugging
            double error = Math.Abs(estimatedTime - client.localTimeline);
            Debug.Log($"CmdClicked: serverTime={NetworkTime.localTime:F3} clientTime={client.localTimeline:F3} estimatedTime={estimatedTime:F3} estimationError={error:F3} position={position}");

            // sample the history to get the nearest snapshots around 'timestamp'
            if (LagCompensation.Sample(history, estimatedTime, lagCompensationSettings.captureInterval, out resultBefore, out resultAfter, out double t))
            {
                // interpolate to get a decent estimation at exactly 'timestamp'
                resultInterpolated = Capture2D.Interpolate(resultBefore, resultAfter, t);
                resultTime = NetworkTime.localTime;

                // check if there really was a cube at that time and position
                Bounds bounds = new Bounds(resultInterpolated.position, resultInterpolated.size);
                if (bounds.Contains(position))
                {
                    return true;
                }
                else Debug.Log($"CmdClicked: interpolated={resultInterpolated} doesn't contain {position}");
            }
            else Debug.Log($"CmdClicked: history doesn't contain {estimatedTime:F3}");

            return false;
        }

        void OnDrawGizmos()
        {
            // should we apply special colors to an active result?
            bool showResult = NetworkTime.localTime <= resultTime + resultDuration;

            // draw interpoalted result first.
            // history meshcubes should write over it for better visibility.
            if (showResult)
            {
                Gizmos.color = Color.black;
                Gizmos.DrawCube(resultInterpolated.position, resultInterpolated.size);
            }

            // draw history
            Gizmos.color = historyColor;
            LagCompensation.DrawGizmos(history);

            // draw result samples after. useful to see the selection process.
            if (showResult)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(resultBefore.position, resultBefore.size);
                Gizmos.DrawWireCube(resultAfter.position, resultAfter.size);
            }
        }
    }
}
