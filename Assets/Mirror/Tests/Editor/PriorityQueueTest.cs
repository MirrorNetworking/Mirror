using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Mirror.KCP;
using System.Linq;

namespace Mirror.Tests
{
    public class PriorityQueueTest
    {
        PriorityQueue<int, string> pqueue;

        [SetUp]
        public void Setup()
        {
            pqueue = new PriorityQueue<int, string>();
        }

        [Test]
        public void Empty()
        {
            Assert.That(pqueue.Count, Is.Zero);
        }

        [Test]
        public void Enqueue()
        {
            pqueue.Enqueue(10, "10");

            Assert.That(pqueue.Count, Is.EqualTo(1));
        }

        [Test]
        public void Dequeue()
        {
            int key = Random.Range(0, int.MaxValue);
            pqueue.Enqueue(key, "" + key);

            (int priority, string value) = pqueue.Dequeue();

            Assert.That(priority, Is.EqualTo(key));
            Assert.That(value, Is.EqualTo("" + key));

        }

        [Test]
        public void Peek()
        {
            int key = Random.Range(0, int.MaxValue);
            pqueue.Enqueue(key, "" + key);

            (int priority, string value) = pqueue.Peek();

            Assert.That(priority, Is.EqualTo(key));
            Assert.That(value, Is.EqualTo("" + key));

        }

        [Test]
        public void DequeueRemoves()
        {
            pqueue.Enqueue(1, "1");
            pqueue.Dequeue();
            Assert.That(pqueue.Count, Is.Zero);
        }

        [Test]
        public void EnqueueSorts()
        {
            // does not matter in which order we insert entries
            pqueue.Enqueue(3, "3");
            pqueue.Enqueue(4, "4");
            pqueue.Enqueue(2, "2");
            pqueue.Enqueue(1, "1");
            pqueue.Enqueue(5, "5");

            // they should dequeue in order of priority
            (int p, string v) = pqueue.Dequeue();

            Assert.That(p, Is.EqualTo(1));
            Assert.That(v, Is.EqualTo("1"));

        }

        [Test]
        public void DequeMaintainsSort()
        {
            // random shuffle 1-10
            List<int> numberList = Enumerable.Range(0, 10).OrderBy(x => Random.value).ToList();
            foreach (int value in numberList)
            {
                pqueue.Enqueue(value, value.ToString());
            }

            for (int i = 0; i < 10; i++)
            {
                // they should dequeue in order of priority
                (int p, string v) = pqueue.Dequeue();

                Assert.That(p, Is.EqualTo(i));
                Assert.That(v, Is.EqualTo(i.ToString()));
            }
        }
    }
}
