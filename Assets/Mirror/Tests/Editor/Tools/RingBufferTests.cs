using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Mirror.Tests.Tools
{
    public class RingBufferTests
    {
        [Test]
        public void Empty()
        {
            RingBuffer<int> ring = new RingBuffer<int>(3);
            Assert.That(ring.Count, Is.EqualTo(0));
        }

        [Test]
        public void Enqueue()
        {
            RingBuffer<int> ring = new RingBuffer<int>(3);
            ring.Enqueue(1337);
            Assert.That(ring.Count, Is.EqualTo(1));
        }

        [Test]
        public void EnqueueMultiple()
        {
            RingBuffer<int> ring = new RingBuffer<int>(3);
            ring.Enqueue(1337);
            ring.Enqueue(1338);
            ring.Enqueue(1339);
            Assert.That(ring.Count, Is.EqualTo(3));
        }

        [Test]
        public void EnqueueDequeue()
        {
            RingBuffer<int> ring = new RingBuffer<int>(3);
            ring.Enqueue(1337);
            Assert.That(ring.Dequeue(), Is.EqualTo(1337));
            Assert.That(ring.Count, Is.EqualTo(0));
        }

        [Test]
        public void DequeueEmpty()
        {
            RingBuffer<int> ring = new RingBuffer<int>(3);
            Assert.Throws<InvalidOperationException>(() => ring.Dequeue());
        }

        [Test]
        public void EnqueueOverCapacity()
        {
            RingBuffer<int> ring = new RingBuffer<int>(3);
            ring.Enqueue(1337);
            ring.Enqueue(1338);
            ring.Enqueue(1339);
            Assert.Throws<InvalidOperationException>(() => ring.Enqueue(1340));
        }

        [Test]
        public void Clear()
        {
            RingBuffer<int> ring = new RingBuffer<int>(3);
            ring.Enqueue(1337);
            ring.Enqueue(1338);
            ring.Enqueue(1339);
            ring.Clear();
            Assert.That(ring.Count, Is.EqualTo(0));
        }

        [Test]
        public void Peek()
        {
            RingBuffer<int> ring = new RingBuffer<int>(3);
            ring.Enqueue(1337);
            ring.Enqueue(1338);
            ring.Enqueue(1339);
            Assert.That(ring.Peek(), Is.EqualTo(1337));
            Assert.That(ring.Count, Is.EqualTo(3));
        }

        [Test]
        public void PeekEmpty()
        {
            RingBuffer<int> ring = new RingBuffer<int>(3);
            Assert.Throws<InvalidOperationException>(() => ring.Peek());
        }

        [Test]
        public void TryPeek()
        {
            RingBuffer<int> ring = new RingBuffer<int>(3);

            // peek empty
            Assert.That(ring.TryPeek(out _), Is.False);
            ring.Enqueue(1337);

            // peek one element
            Assert.That(ring.TryPeek(out int result), Is.True);
            Assert.That(result, Is.EqualTo(1337));

            // peek multiple elements
            ring.Enqueue(1338);
            ring.Enqueue(1339);
            Assert.That(ring.TryPeek(out result), Is.True);
            Assert.That(result, Is.EqualTo(1337));
        }

        [Test]
        public void Tail()
        {
            RingBuffer<int> ring = new RingBuffer<int>(3);
            ring.Enqueue(1337);
            ring.Enqueue(1338);
            ring.Enqueue(1339);
            Assert.That(ring.Tail(), Is.EqualTo(1339));
            Assert.That(ring.Count, Is.EqualTo(3));
        }

        [Test]
        public void TailEmpty()
        {
            RingBuffer<int> ring = new RingBuffer<int>(3);
            Assert.Throws<InvalidOperationException>(() => ring.Tail());
        }

        [Test]
        public void TryTail()
        {
            RingBuffer<int> ring = new RingBuffer<int>(3);

            // empty
            Assert.That(ring.TryTail(out _), Is.False);

            // one element
            ring.Enqueue(1337);
            Assert.That(ring.TryTail(out int result), Is.True);
            Assert.That(result, Is.EqualTo(1337));

            // multiple elements
            ring.Enqueue(1338);
            ring.Enqueue(1339);
            Assert.That(ring.TryTail(out result), Is.True);
            Assert.That(result, Is.EqualTo(1339));
        }

        [Test]
        public void GetElement()
        {
            RingBuffer<int> ring = new RingBuffer<int>(3);
            ring.Enqueue(1337);
            ring.Enqueue(1338);
            ring.Enqueue(1339);
            Assert.That(ring[0], Is.EqualTo(1337));
            Assert.That(ring[1], Is.EqualTo(1338));
            Assert.That(ring[2], Is.EqualTo(1339));
        }

        [Test]
        public void GetElementInvalidIndex()
        {
            RingBuffer<int> ring = new RingBuffer<int>(3);
            Assert.Throws<IndexOutOfRangeException>(() => _ = ring[0]);
            ring.Enqueue(1337);
            ring.Enqueue(1338);
            ring.Enqueue(1339);
            Assert.Throws<IndexOutOfRangeException>(() => _ = ring[3]);
        }

        [Test]
        public void SetElement()
        {
            RingBuffer<int> ring = new RingBuffer<int>(3);
            ring.Enqueue(1337);
            ring.Enqueue(1338);
            ring.Enqueue(1339);
            ring[0] = 10;
            ring[1] = 20;
            ring[2] = 30;
            Assert.That(ring[0], Is.EqualTo(10));
            Assert.That(ring[1], Is.EqualTo(20));
            Assert.That(ring[2], Is.EqualTo(30));
        }

        [Test]
        public void SetElementInvalidIndex()
        {
            RingBuffer<int> ring = new RingBuffer<int>(3);
            Assert.Throws<IndexOutOfRangeException>(() => ring[0] = 1337);
            ring.Enqueue(1337);
            ring.Enqueue(1338);
            ring.Enqueue(1339);
            Assert.Throws<IndexOutOfRangeException>(() => ring[3] = 1340);
        }

        [Test]
        public void ForEach()
        {
            List<int> output = new List<int>();
            RingBuffer<int> ring = new RingBuffer<int>(3);
            ring.Enqueue(1337);
            ring.Enqueue(1338);
            ring.Enqueue(1339);
            foreach (int item in ring)
            {
                output.Add(item);
            }
            Assert.That(output, Is.EquivalentTo(new[] { 1337, 1338, 1339 }));
        }

        [Test]
        public void ForInt()
        {
            List<int> output = new List<int>();
            RingBuffer<int> ring = new RingBuffer<int>(3);
            ring.Enqueue(1337);
            ring.Enqueue(1338);
            ring.Enqueue(1339);
            for (int i = 0; i < ring.Count; ++i)
            {
                output.Add(ring[i]);
            }
            Assert.That(output, Is.EquivalentTo(new[] { 1337, 1338, 1339 }));
        }
    }
}
