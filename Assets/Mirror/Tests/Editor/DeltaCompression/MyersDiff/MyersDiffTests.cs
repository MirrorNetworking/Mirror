// original paper: http://www.xmailserver.org/diff2.pdf
// used in diff, git!

using System;
using MyersDiff;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.DeltaCompression
{
    public class MyersDiffTests : DeltaCompressionTests
    {
        public override void ComputeDelta(NetworkWriter from, NetworkWriter to, NetworkWriter result)
        {
            // algorithm needs int[] for now
            byte[] fromBytes = from.ToArray();
            int[] fromInts = new int[fromBytes.Length];
            for (int i = 0; i < fromBytes.Length; ++i)
                fromInts[i] = fromBytes[i];

            byte[] toBytes = to.ToArray();
            int[] toInts = new int[toBytes.Length];
            for (int i = 0; i < toBytes.Length; ++i)
                toInts[i] = toBytes[i];

            Diff.Item[] items = Diff.DiffInt(fromInts, toInts);
        }

        public override void ApplyPatch(NetworkWriter from, NetworkWriter patch, NetworkWriter result)
        {
            throw new System.NotImplementedException();
        }

        // simple test for understanding
        [Test]
        public void SimpleTest()
        {
            int[] A = {0, 1,    2, 3, 4};
            int[] B = {0, 1, 1, 2,    4};
            Diff.Item[] items = Diff.DiffInt(A, B);

            Debug.Log($"A={String.Join(", ", A)}");
            Debug.Log($"B={String.Join(", ", B)}");
            foreach (Diff.Item item in items)
                Debug.Log($"item: startA={item.StartA} startB={item.StartB} deleteA={item.deletedA} insertB={item.insertedB}");
        }
    }
}
