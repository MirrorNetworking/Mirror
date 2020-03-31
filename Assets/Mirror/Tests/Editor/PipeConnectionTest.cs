using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

using static Mirror.Tests.AsyncUtil;

namespace Mirror.Tests
{
    public class AsyncPipeConnectionTest
    {

        IConnection c1;
        IConnection c2;

        [SetUp]
        public void Setup()
        {
            (c1, c2) = PipeConnection.CreatePipe();
        }

        private static async Task TestSendData(IConnection c1, IConnection c2)
        {
            byte[] data = { 1, 2, 3, 4 };

            await c1.SendAsync(new ArraySegment<byte>(data));
            var memoryStream = new MemoryStream();
            Assert.That(await c2.ReceiveAsync(memoryStream));

            memoryStream.TryGetBuffer(out ArraySegment<byte> receivedData);

            Assert.That(receivedData, Is.EqualTo(receivedData));
        }

        [UnityTest]
        public IEnumerator TestSendAndReceive()
        {
            return RunAsync(async () =>
            {

                await TestSendData(c1, c2);
            });
        }

        [UnityTest]
        public IEnumerator TestSendAndReceiveBackwards()
        {
            return RunAsync(async () =>
            {
                await TestSendData(c2, c1);
            });
        }

        [UnityTest]
        public IEnumerator TestSendAndReceiveMultiple()
        {
            return RunAsync(async () =>
            {
                await TestSendData(c1, c2);
                await TestSendData(c1, c2);
            });
        }

        [UnityTest]
        public IEnumerator TestDisconnectC1()
        {
            return RunAsync(async () =>
            {
                // disconnecting c1 should disconnect both
                c1.Disconnect();

                var memoryStream = new MemoryStream();
                Assert.That(await c1.ReceiveAsync(memoryStream), Is.False);
                Assert.That(await c2.ReceiveAsync(memoryStream), Is.False);
            });
        }

        [UnityTest]
        public IEnumerator TestDisconnectC2()
        {
            return RunAsync(async () =>
            {
                // disconnecting c1 should disconnect both
                c2.Disconnect();

                var memoryStream = new MemoryStream();
                Assert.That(await c1.ReceiveAsync(memoryStream), Is.False);
                Assert.That(await c2.ReceiveAsync(memoryStream), Is.False);
            });
        }

        [Test]
        public void TestAddressC1()
        {
            Assert.That(c1.GetEndPointAddress(), Is.EqualTo(new IPEndPoint(IPAddress.Loopback, 0)));
        }

        [Test]
        public void TestAddressC2()
        {
            Assert.That(c2.GetEndPointAddress(), Is.EqualTo(new IPEndPoint(IPAddress.Loopback, 0)));
        }

    }
}
