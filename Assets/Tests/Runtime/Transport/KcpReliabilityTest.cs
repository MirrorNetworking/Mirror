using System.Collections;
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
        CancellationTokenSource cts;


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
        private async UniTaskVoid SendAsync(Kcp target, byte[] data, int length, CancellationToken token)
        {
            // drop some packets
            if (Random.value < pdrop)
                return;

            int latency = Random.Range(0, maxLat);

            if (latency > 0)
                await UniTask.Delay(latency, false, PlayerLoopTiming.Update, token);

            target.Input(data, 0, length, true, false);

            // duplicate some packets (udp can duplicate packets)
            if (Random.value < pdup)
                target.Input(data, 0, length, true, false);
        }

        async UniTaskVoid Tick(Kcp kcp, CancellationToken token)
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
            cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;

            client = new Kcp(0, (data, length) => {
                SendAsync(server, data, length, token).Forget();
            });
            // fast mode so that we finish quicker
            client.SetNoDelay(KcpDelayMode.Fast3);
            client.SetMtu(1000);
            client.SetWindowSize(16, 16);

            server = new Kcp(0, (data, length) => {
                SendAsync(client, data, length, token).Forget();
            });
            // fast mode so that we finish quicker
            server.SetNoDelay(KcpDelayMode.Fast3);
            client.SetMtu(1000);
            client.SetWindowSize(16, 16);


            Tick(server, token).Forget();
            Tick(client, token).Forget();
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
                byte[] data = { i };
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

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator TestFragmentation() => UniTask.ToCoroutine(async () =>
        {
            // Can't send arbitrarily large
            // messages,  they have to fit in the window
            // so send something larger than window but smaller than MTU * window
            byte[] data = new byte[10000];

            // fill up with some data
            for (int i = 0; i< data.Length; i++)
            {
                data[i] = (byte)(i & 0xFF);
            }

            client.Send(data, 0, data.Length);

            // receive the packet, it should be reassembled as a single packet
            byte[] buffer = new byte[data.Length];

            int size = server.Receive(buffer, 0, buffer.Length);
            while (size < 0)
            {
                await UniTask.Delay(10);
                size = server.Receive(buffer, 0, buffer.Length);
            }

            Assert.That(data, Is.EqualTo(buffer));

        });
    }
}
