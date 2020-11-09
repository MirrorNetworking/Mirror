using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using Random = UnityEngine.Random;

namespace Mirror.Tests
{
    public class KcpFragmentationTest : KcpSetup
    {

        private static byte[] MakeRandomData(int size)
        {
            byte[] data = new byte[size];
            for (int i = 0; i< size; i++)
            {
                data[i] = (byte)Random.Range(0, 255);
            }
            return data;
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator TestSendAndReceive() => UniTask.ToCoroutine(async () =>
        {
            for (int i = 950; i < 1100; i++)
            {
                byte[] data = MakeRandomData(i);
                client.Send(data, 0, data.Length);

                // receive the packets,  they should come in the same order as received
                byte[] buffer = new byte[data.Length];

                // wait for data
                while (server.Receive(buffer) < 0)
                    await UniTask.Delay(10);

                Assert.That(buffer, Is.EqualTo(data));
            }
        });
    }
}