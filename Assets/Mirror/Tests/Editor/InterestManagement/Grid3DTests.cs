using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.InterestManagement
{
    public class Grid3DTests
    {
        Grid3D<int> grid;

        [SetUp]
        public void SetUp()
        {
            grid = new Grid3D<int>(10);
        }

        [Test]
        public void AddAndGetNeighbours()
        {
            // add two at (0, 0, 0)
            grid.Add(Vector3Int.zero, 1);
            grid.Add(Vector3Int.zero, 2);
            HashSet<int> result = new HashSet<int>();
            grid.GetWithNeighbours(Vector3Int.zero, result);
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result.Contains(1), Is.True);
            Assert.That(result.Contains(2), Is.True);

            // add a neighbour at (1, 0, 1)
            grid.Add(new Vector3Int(1, 0, 1), 3);
            grid.GetWithNeighbours(Vector3Int.zero, result);
            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result.Contains(1), Is.True);
            Assert.That(result.Contains(2), Is.True);
            Assert.That(result.Contains(3), Is.True);
            
            // add a neighbour at (1, 1, 1) to test upper layer
            grid.Add(new Vector3Int(1, 1, 1), 4);
            grid.GetWithNeighbours(Vector3Int.zero, result);
            Assert.That(result.Count, Is.EqualTo(4));
            Assert.That(result.Contains(1), Is.True);
            Assert.That(result.Contains(2), Is.True);
            Assert.That(result.Contains(3), Is.True);
            Assert.That(result.Contains(4), Is.True);
            
            // add a neighbour at (1, -1, 1) to test upper layer
            grid.Add(new Vector3Int(1, -1, 1), 5);
            grid.GetWithNeighbours(Vector3Int.zero, result);
            Assert.That(result.Count, Is.EqualTo(5));
            Assert.That(result.Contains(1), Is.True);
            Assert.That(result.Contains(2), Is.True);
            Assert.That(result.Contains(3), Is.True);
            Assert.That(result.Contains(4), Is.True);
            Assert.That(result.Contains(5), Is.True);
        }

        [Test]
        public void GetIgnoresTooFarNeighbours()
        {
            // add at (0, 0, 0)
            grid.Add(Vector3Int.zero, 1);

            // get at (2, 0, 0) which is out of 9 neighbour radius
            HashSet<int> result = new HashSet<int>();
            grid.GetWithNeighbours(new Vector3Int(2, 0, 0), result);
            Assert.That(result.Count, Is.EqualTo(0));
        }

        [Test]
        public void ClearNonAlloc()
        {
            // add some
            grid.Add(Vector3Int.zero, 1);
            grid.Add(Vector3Int.zero, 2);

            // clear and check if empty now
            grid.ClearNonAlloc();
            HashSet<int> result = new HashSet<int>();
            grid.GetWithNeighbours(Vector3Int.zero, result);
            Assert.That(result.Count, Is.EqualTo(0));
        }
    }
}
