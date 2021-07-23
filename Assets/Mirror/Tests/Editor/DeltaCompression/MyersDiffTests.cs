// original paper: http://www.xmailserver.org/diff2.pdf
// used in diff, git!
//
// VARINT before/after:
//   Delta_Tiny:   17 bytes =>  5 bytes
//   Delta_Small:  83 bytes => 26 bytes
//   Delta_Big:   318 bytes => 99 bytes
//
// BENCHMARK 100k BIG CHANGE before/after:
//   original (int[], allocations):   3487ms
//   MyersDiffX V0.2 (<T>, nonalloc): 5000ms ????
//
// BENCHMARK 100k TINY CHANGE:
//   MyersDiffX V0.2 (<T>, nonalloc):  342ms
using System;
using System.Collections.Generic;
using MyersDiffX;

namespace Mirror.Tests.DeltaCompression
{
    public class MyersDiffTests : DeltaCompressionTests
    {
        public override void ComputeDelta(NetworkWriter from, NetworkWriter to, NetworkWriter result)
        {
            // prepare caches
            List<bool> modifiedA = new List<bool>();
            List<bool> modifiedB = new List<bool>();
            List<int> DownVector = new List<int>();
            List<int> UpVector = new List<int>();
            List<Item> diffs = new List<Item>();
            ComputeDeltaNonAlloc(from, to, result, modifiedA, modifiedB, DownVector, UpVector, diffs);
        }

        // NonAlloc version for benchmark
        public void ComputeDeltaNonAlloc(NetworkWriter from, NetworkWriter to, NetworkWriter result,
            List<bool> modifiedA, List<bool> modifiedB,
            List<int> DownVector, List<int> UpVector,
            List<Item> diffs)
        {
            ArraySegment<byte> fromSegment = from.ToArraySegment();
            ArraySegment<byte> toSegment = to.ToArraySegment();

            // myers diff nonalloc
            MyersDiffX.MyersDiffX.DiffNonAlloc(
                fromSegment, toSegment,
                modifiedA, modifiedB,
                DownVector, UpVector,
                diffs
            );
            //foreach (Diff.Item item in diffs)
            //    UnityEngine.Debug.Log($"item: startA={item.StartA} startB={item.StartB} deletedA={item.deletedA} insertedB={item.insertedB}");

            // make patch
            // TODO linked list etc.
            MyersDiffXPatching.MakePatch(fromSegment, toSegment, diffs, result);
        }

        public override void ApplyPatch(NetworkWriter A, NetworkReader delta, NetworkWriter result) =>
            MyersDiffXPatching.ApplyPatch(A, delta, result);

        // overwrite benchmark functon to use NonAlloc version & caches
        public override void ComputeDeltaBenchmark(NetworkWriter writerA, NetworkWriter writerB, int amount, NetworkWriter result)
        {
            //Debug.Log($"Running NonAlloc benchmark...");

            // prepare caches
            List<bool> modifiedA = new List<bool>();
            List<bool> modifiedB = new List<bool>();
            List<int> DownVector = new List<int>();
            List<int> UpVector = new List<int>();
            List<Item> diffs = new List<Item>();

            for (int i = 0; i < amount; ++i)
            {
                // reset write each time. don't want to measure resizing.
                result.Position = 0;
                ComputeDeltaNonAlloc(writerA, writerB, result, modifiedA, modifiedB, DownVector, UpVector, diffs);
            }
        }
    }
}
