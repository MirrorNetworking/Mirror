using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Mirror.Tests
{

    [TestFixture(0f, 0f, 0)]
    [TestFixture(0.2f, 0f, 0)]
    [TestFixture(0f, 0.2f, 0)]
    [TestFixture(0.2f, 0.2f, 0)]
    [TestFixture(0f, 0f, 20)]
    [TestFixture(0.2f, 0.2f, 20)]
    public class KcpReliabilityTest : KcpSetup
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="pdrop"> probability of dropping a packet</param>
        /// <param name="pdup"> probability of duplicating a packet</param>
        /// <param name="minLat">minimum latency of a packet</param>
        /// <param name="maxLat">maximum latency of a packet</param>
        public KcpReliabilityTest(float pdrop, float pdup, int maxLat) : base(pdrop, pdup, maxLat)
        {
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
