using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Mirror.KCP;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests
{

    [TestFixture(0f, 0f, 0)]
    [TestFixture(0.2f, 0f, 0)]
    [TestFixture(0f, 0.2f, 0)]
    [TestFixture(0.2f, 0.2f, 0)]
    [TestFixture(0f, 0f, 20)]
    [TestFixture(0.2f, 0.2f, 20)]
    public class KcpReliabilityTest
    {
        private readonly float pdrop;
        private readonly float pdup;
        private readonly int maxLat;
        Kcp client;
        Kcp server;
        readonly CancellationTokenSource cts = new CancellationTokenSource();


        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdrop"> probability of dropping a packet</param>
        /// <param name="pdup"> probability of duplicating a packet</param>
        /// <param name="minLat">minimum latency of a packet</param>
        /// <param name="maxLat">maximum latency of a packet</param>
        public KcpReliabilityTest(float pdrop, float pdup, int maxLat)
        {
            this.pdrop = pdrop;
            this.pdup = pdup;
            this.maxLat = maxLat;
        }

        /// <summary>
        /// sends a packet to a kcp, simulating unreliable network
        /// </summary>
        /// <param name="target"></param>
        /// <param name="data"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        private async UniTask SendAsync(Kcp target, byte[] data, int length)
        {
            // drop some packets
            if (Random.value < pdrop)
                return;

            int latency = Random.Range(0, maxLat);

            await UniTask.Delay(latency);
            target.Input(data, 0, length, true, false);

            // duplicate some packets (udp can duplicate packets)
            if (Random.value < pdup)
                target.Input(data, 0, length, true, false);
        }

        async UniTask Tick(Kcp kcp, CancellationToken token)
        {
            while (true)
            {
                await UniTask.Delay(10, false, PlayerLoopTiming.Update, token);
                kcp.Update();
            }
        }

        // A Test behaves as an ordinary method
        [SetUp]
        public void SetupKcp()
        {
            client = new Kcp(0, (data, length) => {
                _ = SendAsync(server, data, length);
            });
            // fast mode so that we finish quicker
            client.SetNoDelay(true, 10, 2, true);

            server = new Kcp(0, (data, length) => {
                _ = SendAsync(client, data, length);
            });
            // fast mode so that we finish quicker
            server.SetNoDelay(true, 10, 2, true);

            _ = Tick(server, cts.Token);
            _ = Tick(client, cts.Token);
        }

        [TearDown]
        public void TearDown()
        {
            cts.Cancel();
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator TestSendingData() => UniTask.ToCoroutine(async () =>
        {
            // send 100 packets
            for (byte i=0; i< 50; i ++)
            {
                byte[] data = new byte[] { i };
                client.Send(data, 0, data.Length);
            }

            // receive the packets,  they should come in the same order as received
            byte[] buffer = { 0 };

            for (byte i=0; i< 50; i++)
            {
                // wait for data
                while (server.Receive(buffer, 0, 1) < 0)
                    await UniTask.Delay(10);

                Assert.That(buffer[0], Is.EqualTo(i));
            }
        });
    }
}
