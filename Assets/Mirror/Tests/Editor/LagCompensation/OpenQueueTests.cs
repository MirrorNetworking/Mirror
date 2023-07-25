using System;
using NUnit.Framework;

namespace Mirror.Tests.LagCompensationTests
{
    public class OpenQueueTests
    {
        const int capacity = 4;
        OpenQueue<int> queue;

        [SetUp]
        public void SetUp()
        {
            queue = new OpenQueue<int>(4);
        }

        [Test]
        public void Enqueue()
        {
            queue.Enqueue(1);
            queue.Enqueue(2);
            queue.Enqueue(3);
            queue.Enqueue(4);
            Assert.That(queue.Count, Is.EqualTo(4));
        }

        [Test]
        public void Enqueue_Overflow()
        {
            queue.Enqueue(1);
            queue.Enqueue(2);
            queue.Enqueue(3);
            queue.Enqueue(4);
            Assert.Throws<InvalidOperationException>(() =>
            {
                queue.Enqueue(5);
            });
        }

        [Test]
        public void Dequeue()
        {
            queue.Enqueue(1);
            queue.Enqueue(2);
            queue.Enqueue(3);
            queue.Enqueue(4);
            Assert.That(queue.Dequeue(), Is.EqualTo(1));
            Assert.That(queue.Dequeue(), Is.EqualTo(2));
            Assert.That(queue.Dequeue(), Is.EqualTo(3));
            Assert.That(queue.Dequeue(), Is.EqualTo(4));
            Assert.That(queue.Count, Is.EqualTo(0));
        }

        [Test]
        public void Dequeue_Empty()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                queue.Dequeue();
            });
        }

        [Test]
        public void Peek()
        {
            queue.Enqueue(1);
            queue.Enqueue(2);
            queue.Enqueue(3);
            queue.Enqueue(4);
            Assert.That(queue.Peek(), Is.EqualTo(1));
            Assert.That(queue.Count, Is.EqualTo(4));
        }

        [Test]
        public void Peek_Empty()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                queue.Peek();
            });
        }

        [Test]
        public void Clear()
        {
            queue.Enqueue(1);
            queue.Enqueue(2);
            queue.Enqueue(3);
            queue.Enqueue(4);
            queue.Clear();
            Assert.That(queue.Count, Is.EqualTo(0));
        }
    }
}
