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
using NUnit.Framework;
using Unity.Collections;

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
            List<bool> modifiedA = new List<bool>(A.Length + 2);
            List<bool> modifiedB = new List<bool>(B.Length + 2);

            // need two vectors of size 2 * MAX + 2
            int MAX = A.Length + B.Length + 1;
            // vector for the (0,0) to (x,y) search
            List<int> DownVector = new List<int>(2 * MAX + 2);
            // vector for the (u,v) to (N,M) search
            List<int> UpVector = new List<int>(2 * MAX + 2);

            // run 1k times
            for (int i = 0; i < 1000; ++i)
                MyersDiffX.MyersDiffX.DiffNonAlloc(new ArraySegment<byte>(A), new ArraySegment<byte>(B), modifiedA, modifiedB, DownVector, UpVector, result);
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
            List<bool> modifiedA = new List<bool>(A.Length + 2);
            List<bool> modifiedB = new List<bool>(B.Length + 2);

            // need two vectors of size 2 * MAX + 2
            int MAX = A.Length + B.Length + 1;
            // vector for the (0,0) to (x,y) search
            List<int> DownVector = new List<int>(2 * MAX + 2);
            // vector for the (u,v) to (N,M) search
            List<int> UpVector = new List<int>(2 * MAX + 2);

            // run 1k times
            for (int i = 0; i < 1000; ++i)
                MyersDiffX.MyersDiffX.DiffNonAlloc(new ArraySegment<byte>(A), new ArraySegment<byte>(B), modifiedA, modifiedB, DownVector, UpVector, result);
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
            List<bool> modifiedA = new List<bool>(A.Length + 2);
            List<bool> modifiedB = new List<bool>(B.Length + 2);

            // need two vectors of size 2 * MAX + 2
            int MAX = A.Length + B.Length + 1;
            // vector for the (0,0) to (x,y) search
            List<int> DownVector = new List<int>(2 * MAX + 2);
            // vector for the (u,v) to (N,M) search
            List<int> UpVector = new List<int>(2 * MAX + 2);

            // run 1k times
            for (int i = 0; i < 1000; ++i)
                MyersDiffX.MyersDiffX.DiffNonAlloc(new ArraySegment<byte>(A), new ArraySegment<byte>(B), modifiedA, modifiedB, DownVector, UpVector, result);
        }

        // NativeArray test to prepare for Burst
        [Test]
        public void Benchmark_30percent_changes_x1000_NativeCollections()
        {
            // prepare a big byte[]
            NativeArray<byte> A = new NativeArray<byte>(1000, Allocator.Persistent);
            NativeArray<byte> B = new NativeArray<byte>(1000, Allocator.Persistent);
            // change 1/3rd of values in B
            for (int i = 0; i < B.Length; ++i)
                if (i % 3 == 0)
                    B[i] = 0xFF;

            // prepare the caches for nonalloc
            // allocate the lists.
            // already with expected capacity to avoid resizing.
            NativeList<Item> result = new NativeList<Item>(1000, Allocator.Persistent);
            NativeList<bool> modifiedA = new NativeList<bool>(A.Length + 2, Allocator.Persistent);
            NativeList<bool> modifiedB = new NativeList<bool>(B.Length + 2, Allocator.Persistent);

            // need two vectors of size 2 * MAX + 2
            int MAX = A.Length + B.Length + 1;
            // vector for the (0,0) to (x,y) search
            NativeList<int> DownVector = new NativeList<int>(2 * MAX + 2, Allocator.Persistent);
            // vector for the (u,v) to (N,M) search
            NativeList<int> UpVector = new NativeList<int>(2 * MAX + 2, Allocator.Persistent);

            // run 1k times
            //for (int i = 0; i < 1000; ++i)
            //    MyersDiffX.MyersDiffX.DiffNonAlloc(new ArraySegment<byte>(A), new ArraySegment<byte>(B), modifiedA, modifiedB, DownVector, UpVector, result);

            // cleanup
            A.Dispose();
            B.Dispose();
            result.Dispose();
            modifiedA.Dispose();
            modifiedB.Dispose();
            DownVector.Dispose();
            UpVector.Dispose();
        }
    }
}
