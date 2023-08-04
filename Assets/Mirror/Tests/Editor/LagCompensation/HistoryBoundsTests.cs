using NUnit.Framework;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Mirror.Tests.LagCompensationTests
{
    public class HistoryBoundsTests
    {
        [SetUp]
        public void SetUp() {}

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
        // Unity 2021.3 LTS, release mode: 100k; limit=8
        //   O(N) Queue<Bounds> implementation:     183 ms
        //   O(N) Queue and recalculate every 2nd:  108 ms
        //   O(N) cache friendly OpenQueue:          98 ms
        //   Buckets of 2:                           79 ms
        //   Buckets of 4:                           54 ms
        //   MinMaxBounds + Bucket of 4:             35 ms
        [Test]
        [TestCase(100_000, 8, 1)]
        [TestCase(100_000, 8, 2)]
        [TestCase(100_000, 8, 4)]
        public void Benchmark(int iterations, int limit, int boundsPerBucket)
        {
            // always use the same seed so we get the same test.
            Random.InitState(0);

            HistoryBounds history = new HistoryBounds(limit, boundsPerBucket);
            for (int i = 0; i < iterations; ++i)
            {
                float min = Random.Range(-1, 1);
                float max = Random.Range(min, 1);
                Bounds bounds = MinMax(min, max);
                history.Insert(bounds);

                // always call .total to include any getter calculations in
                // the benchmark here.
                Bounds total = history.total;
            }
        }

        // straight forward test
        [Test]
        public void Insert_Basic()
        {
            const int limit = 3;
            HistoryBounds history = new HistoryBounds(limit, boundsPerBucket: 1);

            // insert initial [-1, 1].
            // should calculate new bounds == initial.
            history.Insert(MinMax(-1, 1));
            Assert.That(history.boundsCount, Is.EqualTo(1));
            Assert.That(history.total, Is.EqualTo(MinMax(-1, 1)));

            // insert [0, 2]
            // should calculate new bounds == [-1, 2].
            history.Insert(MinMax(0, 2));
            Assert.That(history.boundsCount, Is.EqualTo(2));
            Assert.That(history.total, Is.EqualTo(MinMax(-1, 2)));

            // insert one that's smaller than current bounds [-.5, 0]
            // history needs to contain it even if smaller, because once the oldest
            // largest one gets removed, this one matters too.
            history.Insert(MinMax(-0.5f, 0));
            Assert.That(history.boundsCount, Is.EqualTo(3));
            Assert.That(history.total, Is.EqualTo(MinMax(-1, 2)));

            // insert more than 'limit': [0, 0]
            // the oldest one [-1, 1] should be discarded.
            // new bounds should be [-0.5, 2]
            history.Insert(MinMax(0, 0));
            Assert.That(history.boundsCount, Is.EqualTo(3));
            Assert.That(history.total, Is.EqualTo(MinMax(-0.5f, 2)));
        }

        // player runs in a circles and visits the same areas again.
        // removing oldest should not remove a newer area that's still relevant.
        [Test]
        public void Insert_Revisit()
        {
            const int limit = 3;
            HistoryBounds history = new HistoryBounds(limit, boundsPerBucket: 1);

            // insert initial [-1, 1].
            // should calculate new bounds == initial.
            history.Insert(MinMax(-1, 1));
            Assert.That(history.boundsCount, Is.EqualTo(1));
            Assert.That(history.total, Is.EqualTo(MinMax(-1, 1)));

            // insert [0, 2]
            // should calculate new bounds == [-1, 2].
            history.Insert(MinMax(0, 2));
            Assert.That(history.boundsCount, Is.EqualTo(2));
            Assert.That(history.total, Is.EqualTo(MinMax(-1, 2)));

            // visit [-1, 1] again
            history.Insert(MinMax(-1, 1));
            Assert.That(history.boundsCount, Is.EqualTo(3));
            Assert.That(history.total, Is.EqualTo(MinMax(-1, 2)));

            // insert beyond limit.
            // oldest one [-1, 1] should be removed.
            // total should still include it because we revisited [1, 1].
            history.Insert(MinMax(0, 0));
            Assert.That(history.boundsCount, Is.EqualTo(3));
            Assert.That(history.total, Is.EqualTo(MinMax(-1, 2)));
        }

        // by default, HistoryBounds.total is new Bounds() which is (0,0).
        // make sure this isn't included in results by default.
        [Test]
        public void Insert_Far()
        {
            const int limit = 3;
            HistoryBounds history = new HistoryBounds(limit, boundsPerBucket: 1);

            // insert initial [2, 3].
            // should calculate new bounds == initial.
            history.Insert(MinMax(2, 3));
            Assert.That(history.boundsCount, Is.EqualTo(1));
            Assert.That(history.total, Is.EqualTo(MinMax(2, 3)));

            // insert [3, 4]
            // should calculate new bounds == [2, 4].
            history.Insert(MinMax(3, 4));
            Assert.That(history.boundsCount, Is.EqualTo(2));
            Assert.That(history.total, Is.EqualTo(MinMax(2, 4)));

            // insert one that's smaller than current bounds [0.5, 1]
            // history needs to contain it even if smaller, because once the oldest
            // largest one gets removed, this one matters too.
            history.Insert(MinMax(0.5f, 1));
            Assert.That(history.boundsCount, Is.EqualTo(3));
            Assert.That(history.total, Is.EqualTo(MinMax(0.5f, 4)));

            // insert more than 'limit'
            // the oldest one [-1, 1] should be discarded.
            // new bounds should be [-0.5, 2]
            history.Insert(MinMax(2, 2));
            Assert.That(history.boundsCount, Is.EqualTo(3));
            Assert.That(history.total, Is.EqualTo(MinMax(0.5f, 4)));
        }

        // test to check if bounds per bucket works as expected
        [Test]
        public void Insert_MultipleBoundsPerBucket()
        {
            const int limit = 3;
            HistoryBounds history = new HistoryBounds(limit, boundsPerBucket: 2);

            // simple test in a straight line to see if buckets are in bounds of 2x

            // first insertion into current
            history.Insert(MinMax(1, 1));
            Assert.That(history.total, Is.EqualTo(MinMax(1, 1)));

            // second insertion: current is full, moved to history, cleared
            history.Insert(MinMax(2, 2));
            Assert.That(history.total, Is.EqualTo(MinMax(1, 2)));

            // third insertion into current
            history.Insert(MinMax(3, 3));
            Assert.That(history.total, Is.EqualTo(MinMax(1, 3)));

            // fourth insertion: current is full, moved to history, removed oldest history
            history.Insert(MinMax(4, 4));
            Assert.That(history.total, Is.EqualTo(MinMax(3, 4)));

            // fifth insertion into current
            history.Insert(MinMax(5, 5));
            Assert.That(history.total, Is.EqualTo(MinMax(3, 5)));

            // sixth insertion: current is full, moved to history, removed oldest history
            history.Insert(MinMax(6, 6));
            Assert.That(history.total, Is.EqualTo(MinMax(5, 6)));
        }

        [Test]
        public void Reset()
        {
            const int limit = 3;
            HistoryBounds history = new HistoryBounds(limit, boundsPerBucket: 1);

            history.Insert(MinMax(1, 2));
            history.Insert(MinMax(2, 3));
            history.Insert(MinMax(3, 4));

            history.Reset();
            Assert.That(history.boundsCount, Is.EqualTo(0));
            Assert.That(history.total, Is.EqualTo(MinMax(0, 0)));
        }
    }
}
