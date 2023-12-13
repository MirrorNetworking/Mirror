using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.InterestManagement
{
    public class Grid2DTests
    {
        Grid2D<int> grid;

        [SetUp]
        public void SetUp()
        {
            grid = new Grid2D<int>(10);
        }

        [Test]
        public void AddAndGetNeighbours()
        {
            // add two at (0, 0)
            grid.Add(Vector2Int.zero, 1);
            grid.Add(Vector2Int.zero, 2);
            HashSet<int> result = new HashSet<int>();
            grid.GetWithNeighbours(Vector2Int.zero, result);
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result.Contains(1), Is.True);
            Assert.That(result.Contains(2), Is.True);

            // add a neighbour at (1, 1)
            grid.Add(new Vector2Int(1, 1), 3);
            grid.GetWithNeighbours(Vector2Int.zero, result);
            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result.Contains(1), Is.True);
            Assert.That(result.Contains(2), Is.True);
            Assert.That(result.Contains(3), Is.True);
        }

        [Test]
        public void GetIgnoresTooFarNeighbours()
        {
            // add at (0, 0)
            grid.Add(Vector2Int.zero, 1);

            // get at (2, 0) which is out of 9 neighbour radius
            HashSet<int> result = new HashSet<int>();
            grid.GetWithNeighbours(new Vector2Int(2, 0), result);
            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public void ClearNonAlloc()
        {
            // add some
            grid.Add(Vector2Int.zero, 1);
            grid.Add(Vector2Int.zero, 2);

            // clear and check if empty now
            grid.ClearNonAlloc();
            HashSet<int> result = new HashSet<int>();
            grid.GetWithNeighbours(Vector2Int.zero, result);
            Assert.That(result.Count, Is.EqualTo(0));
        }
    }
}
