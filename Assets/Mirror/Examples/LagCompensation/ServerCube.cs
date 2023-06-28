using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Mirror.Examples.LagCompensationDemo
{
    public class ServerCube : MonoBehaviour
    {
        [Header("Components")]
        public ClientCube client;
        public new BoxCollider collider;

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
        // TODO ringbuffer with time key
        List<KeyValuePair<double, Capture2D>> history = new List<KeyValuePair<double, Capture2D>>();

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

        // [Header("Lag Compensation")]


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
            Capture2D capture = new Capture2D{ position = transform.position };

            // insert into history
            LagCompensation.Insert(history, lagCompensationSettings.historyLimit, NetworkTime.localTime, capture);
        }

        // client says: "I was clicked here, at this time."
        // server needs to rollback to validate.
        // timestamp is the client's snapshot interpolated timeline!
        public bool CmdClicked(double timestamp, Vector2 position)
        {
            Debug.Log($"Server lag compensation: timestamp={timestamp:F3} position={position}");
            return false;
        }

        void OnDrawGizmos()
        {
            // draw mesh cubes of the history, with the current collider's size
            foreach (KeyValuePair<double, Capture2D> kvp in history)
            {
                Gizmos.color = historyColor;
                Gizmos.DrawWireCube(kvp.Value.position, collider.size);
            }

            // draw latest result
            if (NetworkTime.localTime <= resultTime + resultDuration)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawCube(resultBefore.position, collider.size);
                Gizmos.DrawCube(resultAfter.position, collider.size);
                Gizmos.color = Color.magenta;
                Gizmos.DrawCube(resultInterpolated.position, collider.size);
            }
        }
    }
}
