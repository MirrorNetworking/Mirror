using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.Prediction
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

        [Test]
        public void Insert()
        {
            const int limit = 3;

            // insert initial [-1, 1].
            // should calculate new bounds == initial.
            Bounds total = HistoryBounds.Insert(history, limit, MinMax(-Vector3.one, Vector3.one));
            Assert.That(history.Count, Is.EqualTo(1));
            Assert.That(total, Is.EqualTo(MinMax(-Vector3.one, Vector3.one)));

            // insert [0, 2]
            // should calculate new bounds == [-1, 2].
            total = HistoryBounds.Insert(history, limit, MinMax(Vector3.zero, Vector3.one * 2));
            Assert.That(history.Count, Is.EqualTo(2));
            Assert.That(total, Is.EqualTo(MinMax(-Vector3.one, Vector3.one * 2)));

            // insert one that's smaller than current bounds [-.5, 0]
            // history needs to contain it even if smaller, because once the oldest
            // largest one gets removed, this one matters too.
            total = HistoryBounds.Insert(history, limit, MinMax(-Vector3.one / 2, Vector3.zero));
            Assert.That(history.Count, Is.EqualTo(3));
            Assert.That(total, Is.EqualTo(MinMax(-Vector3.one, Vector3.one * 2)));

            // insert more than 'limit': [0, 0]
            // the oldest one [-1, 1] should be discarded.
            // new bounds should be [-0.5, 2]
            total = HistoryBounds.Insert(history, limit, MinMax(Vector3.zero, Vector3.zero));
            Assert.That(history.Count, Is.EqualTo(3));
            Assert.That(total, Is.EqualTo(MinMax(-Vector3.one / 2, Vector3.one * 2)));
        }
    }
}
