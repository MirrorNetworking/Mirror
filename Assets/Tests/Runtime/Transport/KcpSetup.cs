using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using Mirror.KCP;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class KcpSetup : MonoBehaviour
    {
        public readonly float pdrop;
        public readonly float pdup;
        public readonly int maxLat;
        public Kcp client;
        public Kcp server;
        public CancellationTokenSource cts;

        Stopwatch stopwatch = new Stopwatch();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdrop"> probability of dropping a packet</param>
        /// <param name="pdup"> probability of duplicating a packet</param>
        /// <param name="minLat">minimum latency of a packet</param>
        /// <param name="maxLat">maximum latency of a packet</param>
        public KcpSetup(float pdrop = 0, float pdup = 0, int maxLat = 0)
        {
            this.pdrop = pdrop;
            this.pdup = pdup;
            this.maxLat = maxLat;
            stopwatch.Start();
        }

        /// <summary>
        /// sends a packet to a kcp, simulating unreliable network
        /// </summary>
        /// <param name="target"></param>
        /// <param name="data"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public virtual async UniTaskVoid SendAsync(Kcp target, byte[] data, int length, CancellationToken token)
        {
            // drop some packets
            if (Random.value < pdrop)
                return;

            int latency = Random.Range(0, maxLat);

            if (latency > 0)
                await UniTask.Delay(latency, false, PlayerLoopTiming.Update, token);

            target.Input(data, length);

            // duplicate some packets (udp can duplicate packets)
            if (Random.value < pdup)
                target.Input(data, length);
        }

        public virtual async UniTaskVoid Tick(Kcp kcp, CancellationToken token)
        {
            while (true)
            {
                await UniTask.Delay(10, false, PlayerLoopTiming.Update, token);

                kcp.Update((uint)stopwatch.ElapsedMilliseconds);
            }
        }

        // A Test behaves as an ordinary method
        [SetUp]
        public void SetupKcp()
        {
            cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;

            client = new Kcp(0, (data, length) =>
            {
                SendAsync(server, data, length, token).Forget();
            });
            // fast mode so that we finish quicker
            client.SetNoDelay(KcpDelayMode.Fast3);
            client.Mtu = 1000;
            client.SetWindowSize(16, 16);

            server = new Kcp(0, (data, length) =>
            {
                SendAsync(client, data, length, token).Forget();
            });
            // fast mode so that we finish quicker
            server.SetNoDelay(KcpDelayMode.Fast3);
            client.Mtu = 1000;
            client.SetWindowSize(16, 16);


            Tick(server, token).Forget();
            Tick(client, token).Forget();
        }

        [TearDown]
        public void TearDown()
        {
            cts.Cancel();
        }
    }
}
