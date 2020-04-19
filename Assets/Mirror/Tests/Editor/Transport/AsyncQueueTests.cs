using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

using static Mirror.Tests.AsyncUtil;

namespace Mirror.Tests
{
    public class AsyncQueueTests
    {

        // A UnityTest behaves like a coroutine in Play Mode. In Edit Mode you can use
        // `yield return null;` to skip a frame.
        [UnityTest]
        public IEnumerator QueueDequeu() => RunAsync(async () =>
        {
            var asyncQueue = new AsyncQueue<int>();

            asyncQueue.Equeue(3);

            int result = await asyncQueue.DequeueAsync();

            Assert.That(result, Is.EqualTo(3));
        });

        [UnityTest]
        public IEnumerator EnqueueShouldWait() => RunAsync(async () =>
        {
            var asyncQueue = new AsyncQueue<int>();

            Task<int> task1 = asyncQueue.DequeueAsync();

            Assert.That(task1.IsCompleted, Is.False);

            asyncQueue.Equeue(1);

            Assert.That(await task1, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator FirstInFirstOut() => RunAsync(async () =>
        {
            var asyncQueue = new AsyncQueue<int>();

            asyncQueue.Equeue(1);
            asyncQueue.Equeue(2);

            Assert.That(await asyncQueue.DequeueAsync(), Is.EqualTo(1));
            Assert.That(await asyncQueue.DequeueAsync(), Is.EqualTo(2));
        });
    }
}
