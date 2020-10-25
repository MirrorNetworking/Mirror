using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
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

        private static UniTask SendData(IConnection c, byte[] data)
        {
            return c.SendAsync(new ArraySegment<byte>(data));
        }


        private static async Task ExpectData(IConnection c, byte[] expected)
        {
            var memoryStream = new MemoryStream();
            await c.ReceiveAsync(memoryStream);

            memoryStream.TryGetBuffer(out ArraySegment<byte> receivedData);
            Assert.That(receivedData, Is.EqualTo(new ArraySegment<byte>(expected)));
        }

        [UnityTest]
        public IEnumerator TestSendAndReceive() => RunAsync(async () =>
        {
            await SendData(c1, new byte[] { 1, 2, 3, 4 });

            await ExpectData(c2, new byte[] { 1, 2, 3, 4 });
        });

        [UnityTest]
        public IEnumerator TestSendAndReceiveMultiple() => RunAsync(async () =>
        {
            await SendData(c1, new byte[] { 1, 2, 3, 4 });
            await SendData(c1, new byte[] { 5, 6, 7, 8 });

            await ExpectData(c2, new byte[] { 1, 2, 3, 4 });
            await ExpectData(c2, new byte[] { 5, 6, 7, 8 });
        });

        [UnityTest]
        public IEnumerator TestDisconnectC1() => RunAsync(async () =>
        {
            // disconnecting c1 should disconnect both
            c1.Disconnect();

            var memoryStream = new MemoryStream();
            try
            {
                await c1.ReceiveAsync(memoryStream);
                Assert.Fail("Recive Async should have thrown EndOfStreamException");
            }
            catch (EndOfStreamException)
            {
                // good to go
            }

            try
            {
                await c2.ReceiveAsync(memoryStream);
                Assert.Fail("Recive Async should have thrown EndOfStreamException");
            }
            catch (EndOfStreamException)
            {
                // good to go
            }
        });

        [UnityTest]
        public IEnumerator TestDisconnectC2() => RunAsync(async () =>
        {
            // disconnecting c1 should disconnect both
            c2.Disconnect();

            var memoryStream = new MemoryStream();
            try
            {
                await c1.ReceiveAsync(memoryStream);
                Assert.Fail("Recive Async should have thrown EndOfStreamException");
            }
            catch (EndOfStreamException)
            {
                // good to go
            }

            try
            {
                await c2.ReceiveAsync(memoryStream);
                Assert.Fail("Recive Async should have thrown EndOfStreamException");
            }
            catch (EndOfStreamException)
            {
                // good to go
            }
        });

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
