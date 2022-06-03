using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Examples.SnapshotInterpolation
{
    public class ServerCube : MonoBehaviour
    {
        [Header("Components")]
        public ClientCube client;

        [Header("Movement")]
        public float distance = 10;
        public float speed = 3;
        Vector3 start;

        [Header("Snapshot Interpolation")]
        public float sendInterval = 0.05f; // send every 50 ms
        float lastSendTime;

        [Header("Latency Simulation")]
        [Tooltip("Spike latency via perlin(Time * speedMultiplier) * spikeMultiplier")]
        [Range(0, 1)] public float latencySpikeMultiplier = 0.5f;
        [Tooltip("Spike latency via perlin(Time * speedMultiplier) * spikeMultiplier")]
        public float latencySpikeSpeedMultiplier = 0.5f;
        [Tooltip("Packet loss in %")]
        [Range(0, 1)] public float loss = 0.1f;
        [Tooltip("Latency in seconds")]
        public float latency = 0.05f; // 50 ms
        [Tooltip("Scramble % of unreliable messages, just like over the real network. Mirror unreliable is unordered.")]
        [Range(0, 1)] public float scramble = 0.1f;

        // random
        // UnityEngine.Random.value is [0, 1] with both upper and lower bounds inclusive
        // but we need the upper bound to be exclusive, so using System.Random instead.
        // => NextDouble() is NEVER < 0 so loss=0 never drops!
        // => NextDouble() is ALWAYS < 1 so loss=1 always drops!
        System.Random random = new System.Random();

        // hold on to snapshots for a little while to simulate latency
        List<(float, Vector3)> queue = new List<(float, Vector3)>();

        // noise for latency simulation
        static float Noise(float t) => Mathf.PerlinNoise(t, t);

        // latency simulation
        float SimulateLatency()
        {
            // spike over perlin noise.
            // no spikes isn't realistic.
            // sin is too predictable / not realistic.
            // perlin is still deterministic and random enough.
            float spike = Noise(Time.unscaledTime * latencySpikeSpeedMultiplier) * latencySpikeMultiplier;
            return latency + spike;
        }

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
        }

        void Send(Vector3 position)
        {
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
                queue.Insert(index, (Time.time + simulatedLatency, position));
            }
        }

        void Flush()
        {
            // flush ready snapshots to client
            for (int i = 0; i < queue.Count; ++i)
            {
                (float threshold, Vector3 position) = queue[i];
                if (Time.time >= threshold)
                {
                    Snapshot3D snap = new Snapshot3D(Time.time, 0, position);
                    client.OnMessage(snap);
                    queue.RemoveAt(i);
                    --i;
                }
            }
        }
    }
}
