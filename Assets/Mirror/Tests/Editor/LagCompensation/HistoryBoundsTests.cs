using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.LagCompensationTests
{
    public class HistoryBoundsTests
    {
        Queue<Bounds> history;

        [SetUp]
        public void SetUp()
        {
            history = new Queue<Bounds>();
        }

        // helper function to construct (min, max) bounds
        public static Bounds MinMax(Vector3 min, Vector3 max)
        {
            Bounds bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        public static Bounds MinMax(float min, float max) =>
            MinMax(new Vector3(min, min, min), new Vector3(max, max, max));

        // simple benchmark to compare some optimizations later.
        // 64 entries are much more than we would usually use.
        //
        // Unity 2021.3 LTS, release mode, 10_000 x 64 x 8:
        //   native O(N) implementation: 1005 ms
        [Test]
        [TestCase(10_000, 64, 8)]
        public void Benchmark(int iterations, int insertions, int limit)
        {
            // repeat the test 'iterations' x times
            for (int iteration = 0; iteration < iterations; ++iteration)
            {
                // each test captures 'insertions' bounds,
                // with a history of 'limit' bounds.
                history.Clear();
                for (int i = 0; i < insertions; ++i)
                {
                    float min = Random.Range(-1, 1);
                    float max = Random.Range(min, 1);
                    Bounds bounds = MinMax(min, max);
                    Bounds total = HistoryBounds.Insert(history, limit, bounds);
                }
            }
        }

        [Test]
        public void Insert()
        {
            const int limit = 3;

            // insert initial [-1, 1].
            // should calculate new bounds == initial.
            Bounds total = HistoryBounds.Insert(history, limit, MinMax(-1, 1));
            Assert.That(history.Count, Is.EqualTo(1));
            Assert.That(total, Is.EqualTo(MinMax(-1, 1)));

            // insert [0, 2]
            // should calculate new bounds == [-1, 2].
            total = HistoryBounds.Insert(history, limit, MinMax(0, 2));
            Assert.That(history.Count, Is.EqualTo(2));
            Assert.That(total, Is.EqualTo(MinMax(-1, 2)));

            // insert one that's smaller than current bounds [-.5, 0]
            // history needs to contain it even if smaller, because once the oldest
            // largest one gets removed, this one matters too.
            total = HistoryBounds.Insert(history, limit, MinMax(-0.5f, 0));
            Assert.That(history.Count, Is.EqualTo(3));
            Assert.That(total, Is.EqualTo(MinMax(-1, 2)));

            // insert more than 'limit': [0, 0]
            // the oldest one [-1, 1] should be discarded.
            // new bounds should be [-0.5, 2]
            total = HistoryBounds.Insert(history, limit, MinMax(0, 0));
            Assert.That(history.Count, Is.EqualTo(3));
            Assert.That(total, Is.EqualTo(MinMax(-0.5f, 2)));
        }
    }
}