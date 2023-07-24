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

        [Test]
        public void Insert()
        {
            // insert initial. new bounds should be initial.
            Bounds initial = new Bounds(Vector3.zero, Vector3.one);
            Bounds total = HistoryBounds.Insert(history, initial);
            Assert.That(total, Is.EqualTo(initial));
        }
    }
}
