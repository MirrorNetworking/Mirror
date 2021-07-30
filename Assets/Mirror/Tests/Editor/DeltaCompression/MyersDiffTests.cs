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
using System.Collections.Generic;
using MyersDiffX;
using NUnit.Framework;

namespace Mirror.Tests.DeltaCompression
{
    public class MyersDiffTests : DeltaCompressionTests
    {
        // helper function to asset all contents of 'Item'
        public static void AssertItem(Item item, int StartA, int StartB, int deletedA, int insertedB)
        {
            Assert.That(item.StartA, Is.EqualTo(StartA));
            Assert.That(item.StartB, Is.EqualTo(StartB));
            Assert.That(item.deletedA, Is.EqualTo(deletedA));
            Assert.That(item.insertedB, Is.EqualTo(insertedB));
        }

        public override void ComputeDelta(NetworkWriter from, NetworkWriter to, NetworkWriter result)
        {
            // prepare caches
            bool[] modifiedA = new bool[0];
            bool[] modifiedB = new bool[0];
            int[] DownVector = new int[0];
            int[] UpVector = new int[0];
            List<Item> diffs = new List<Item>();
            ComputeDeltaNonAlloc(from, to, result, ref modifiedA, ref modifiedB, ref DownVector, ref UpVector, diffs);
        }

        // NonAlloc version for benchmark
        public void ComputeDeltaNonAlloc(NetworkWriter from, NetworkWriter to, NetworkWriter result,
            ref bool[] modifiedA, ref bool[] modifiedB,
            ref int[] DownVector, ref int[] UpVector,
            List<Item> diffs)
        {
            ArraySegmentX<byte> fromSegment = from.ToArraySegment();
            ArraySegmentX<byte> toSegment = to.ToArraySegment();

            // myers diff nonalloc
            MyersDiffX.MyersDiffX.DiffNonAlloc(
                fromSegment, toSegment,
                ref modifiedA, ref modifiedB,
                ref DownVector, ref UpVector,
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
            bool[] modifiedA = new bool[0];
            bool[] modifiedB = new bool[0];
            int[] DownVector = new int[0];
            int[] UpVector = new int[0];
            List<Item> diffs = new List<Item>();

            for (int i = 0; i < amount; ++i)
            {
                // reset write each time. don't want to measure resizing.
                result.Position = 0;
                ComputeDeltaNonAlloc(writerA, writerB, result, ref modifiedA, ref modifiedB, ref DownVector, ref UpVector, diffs);
            }
        }

        // raw MyersDiffX test for Rider netcore vs. Unity mono comparison.
        // Macbook Air M1.
        //
        // Rider 2021.2 EAP 7 Apple Silicon, Relase Mode: 87 ms
        // Unity 2021.2.0b2 Apple Silicon, Release Mode:  41 ms
        [Test]
        public void Benchmark_NetCore_vs_Mono_1percent_changes_x1000()
        {
            // prepare a big byte[]
            byte[] A = new byte[1000];
            byte[] B = new byte[1000];
            // change 1/3rd of values in B
            for (int i = 0; i < B.Length; ++i)
                if (i % 100 == 0)
                    B[i] = 0xFF;

            // prepare the caches for nonalloc
            // allocate the lists.
            // already with expected capacity to avoid resizing.
            List<Item> result = new List<Item>();
            bool[] modifiedA = new bool[A.Length + 2];
            bool[] modifiedB = new bool[B.Length + 2];

            // need two vectors of size 2 * MAX + 2
            int MAX = A.Length + B.Length + 1;
            // vector for the (0,0) to (x,y) search
            int[] DownVector = new int[2 * MAX + 2];
            // vector for the (u,v) to (N,M) search
            int[] UpVector = new int[2 * MAX + 2];

            // run 1k times
            for (int i = 0; i < 1000; ++i)
                MyersDiffX.MyersDiffX.DiffNonAlloc(new ArraySegmentX<byte>(A), new ArraySegmentX<byte>(B), ref modifiedA, ref modifiedB, ref DownVector, ref UpVector, result);
        }

        // raw MyersDiffX test for Rider netcore vs. Unity mono comparison.
        // Macbook Air M1.
        //
        // Rider 2021.2 EAP 7 Apple Silicon, Relase Mode: 451 ms
        // Unity 2021.2.0b2 Apple Silicon, Release Mode:  225 ms
        [Test]
        public void Benchmark_NetCore_vs_Mono_10percent_changes_x1000()
        {
            // prepare a big byte[]
            byte[] A = new byte[1000];
            byte[] B = new byte[1000];
            // change 1/3rd of values in B
            for (int i = 0; i < B.Length; ++i)
                if (i % 10 == 0)
                    B[i] = 0xFF;

            // prepare the caches for nonalloc
            // allocate the lists.
            // already with expected capacity to avoid resizing.
            List<Item> result = new List<Item>();
            bool[] modifiedA = new bool[A.Length + 2];
            bool[] modifiedB = new bool[B.Length + 2];

            // need two vectors of size 2 * MAX + 2
            int MAX = A.Length + B.Length + 1;
            // vector for the (0,0) to (x,y) search
            int[] DownVector = new int[2 * MAX + 2];
            // vector for the (u,v) to (N,M) search
            int[] UpVector = new int[2 * MAX + 2];

            // run 1k times
            for (int i = 0; i < 1000; ++i)
                MyersDiffX.MyersDiffX.DiffNonAlloc(new ArraySegmentX<byte>(A), new ArraySegmentX<byte>(B), ref modifiedA, ref modifiedB, ref DownVector, ref UpVector, result);
        }

        // raw MyersDiffX test for Rider netcore vs. Unity mono comparison.
        // Macbook Air M1.
        //
        // Rider 2021.2 EAP 7 Apple Silicon, Relase Mode: 3.0s
        // Unity 2021.2.0b2 Apple Silicon, Release Mode:  4.6s
        [Test]
        public void Benchmark_NetCore_vs_Mono_30percent_changes_x1000()
        {
            // prepare a big byte[]
            byte[] A = new byte[1000];
            byte[] B = new byte[1000];
            // change 1/3rd of values in B
            for (int i = 0; i < B.Length; ++i)
                if (i % 3 == 0)
                    B[i] = 0xFF;

            // prepare the caches for nonalloc
            // allocate the lists.
            // already with expected capacity to avoid resizing.
            List<Item> result = new List<Item>();
            bool[] modifiedA = new bool[A.Length + 2];
            bool[] modifiedB = new bool[B.Length + 2];

            // need two vectors of size 2 * MAX + 2
            int MAX = A.Length + B.Length + 1;
            // vector for the (0,0) to (x,y) search
            int[] DownVector = new int[2 * MAX + 2];
            // vector for the (u,v) to (N,M) search
            int[] UpVector = new int[2 * MAX + 2];

            // run 1k times
            for (int i = 0; i < 1000; ++i)
                MyersDiffX.MyersDiffX.DiffNonAlloc(new ArraySegmentX<byte>(A), new ArraySegmentX<byte>(B), ref modifiedA, ref modifiedB, ref DownVector, ref UpVector, result);
        }
    }
}
