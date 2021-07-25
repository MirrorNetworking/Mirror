// burst version for MyersDiff tests
using System;
using MyersDiffX;
using NUnit.Framework;
using Unity.Collections;

namespace Mirror.Tests.DeltaCompression
{
    public class MyersDiffBurstTests : DeltaCompressionTests
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
            // prepare the caches for nonalloc
            // allocate the lists.
            // already with expected capacity to avoid resizing.
            NativeList<Item> diffs = new NativeList<Item>(1000, Allocator.Persistent);
            NativeList<byte> modifiedA = new NativeList<byte>(from.Position + 2, Allocator.Persistent);
            NativeList<byte> modifiedB = new NativeList<byte>(to.Position + 2, Allocator.Persistent);

            // need two vectors of size 2 * MAX + 2
            int MAX = from.Position + to.Position + 1;
            // vector for the (0,0) to (x,y) search
            NativeList<int> DownVector = new NativeList<int>(2 * MAX + 2, Allocator.Persistent);
            // vector for the (u,v) to (N,M) search
            NativeList<int> UpVector = new NativeList<int>(2 * MAX + 2, Allocator.Persistent);

            // prepare caches
            ComputeDeltaNonAlloc(from, to, result, modifiedA, modifiedB, DownVector, UpVector, diffs);

            // cleanup
            diffs.Dispose();
            modifiedA.Dispose();
            modifiedB.Dispose();
            DownVector.Dispose();
            UpVector.Dispose();
        }

        // NonAlloc version for benchmark
        public void ComputeDeltaNonAlloc(NetworkWriter from, NetworkWriter to, NetworkWriter result,
            NativeList<byte> modifiedA, NativeList<byte> modifiedB,
            NativeList<int> DownVector, NativeList<int> UpVector,
            NativeList<Item> diffs)
        {
            ArraySegment<byte> fromSegment = from.ToArraySegment();
            ArraySegment<byte> toSegment = to.ToArraySegment();

            // copy buffers to native array
            // TODO allocs
            NativeArray<byte> A = new NativeArray<byte>(fromSegment.Count, Allocator.Persistent);
            NativeArray<byte> B = new NativeArray<byte>(toSegment.Count, Allocator.Persistent);
            for (int i = 0; i < fromSegment.Count; ++i) A[i] = fromSegment.Array[fromSegment.Offset + i];
            for (int i = 0; i < toSegment.Count; ++i) B[i] = toSegment.Array[toSegment.Offset + i];

            // myers diff nonalloc
            MyersDiffXBurst.DiffNonAlloc(
                A, B,
                modifiedA, modifiedB,
                DownVector, UpVector,
                diffs
            );
            //foreach (Diff.Item item in diffs)
            //    UnityEngine.Debug.Log($"item: startA={item.StartA} startB={item.StartB} deletedA={item.deletedA} insertedB={item.insertedB}");

            // make patch
            // TODO linked list etc.
            MyersDiffXBurstPatching.MakePatch(A, B, diffs, result);

            // cleanup
            A.Dispose();
            B.Dispose();
        }

        public override void ApplyPatch(NetworkWriter A, NetworkReader delta, NetworkWriter result) =>
            MyersDiffXPatching.ApplyPatch(A, delta, result);

        // overwrite benchmark functon to use NonAlloc version & caches
        public override void ComputeDeltaBenchmark(NetworkWriter writerA, NetworkWriter writerB, int amount, NetworkWriter result)
        {
            //Debug.Log($"Running NonAlloc benchmark...");

            // prepare the caches for nonalloc
            // allocate the lists.
            // already with expected capacity to avoid resizing.
            NativeList<Item> diffs = new NativeList<Item>(1000, Allocator.Persistent);
            NativeList<byte> modifiedA = new NativeList<byte>(writerA.Position + 2, Allocator.Persistent);
            NativeList<byte> modifiedB = new NativeList<byte>(writerB.Position + 2, Allocator.Persistent);

            // need two vectors of size 2 * MAX + 2
            int MAX = writerA.Position + writerB.Position + 1;
            // vector for the (0,0) to (x,y) search
            NativeList<int> DownVector = new NativeList<int>(2 * MAX + 2, Allocator.Persistent);
            // vector for the (u,v) to (N,M) search
            NativeList<int> UpVector = new NativeList<int>(2 * MAX + 2, Allocator.Persistent);

            for (int i = 0; i < amount; ++i)
            {
                // reset write each time. don't want to measure resizing.
                result.Position = 0;
                ComputeDeltaNonAlloc(writerA, writerB, result, modifiedA, modifiedB, DownVector, UpVector, diffs);
            }

            // cleanup
            diffs.Dispose();
            modifiedA.Dispose();
            modifiedB.Dispose();
            DownVector.Dispose();
            UpVector.Dispose();
        }

        // NativeArray test to prepare for Burst
        [Test]
        public void Diff_NativeCollections()
        {
            // prepare a big byte[]
            NativeArray<byte> A = new NativeArray<byte>(3, Allocator.Persistent);
            NativeArray<byte> B = new NativeArray<byte>(3, Allocator.Persistent);
            // change one value in B
            B[0] = 0xFF;

            // prepare the caches for nonalloc
            // allocate the lists.
            // already with expected capacity to avoid resizing.
            NativeList<Item> result = new NativeList<Item>(1000, Allocator.Persistent);
            NativeList<byte> modifiedA = new NativeList<byte>(A.Length + 2, Allocator.Persistent);
            NativeList<byte> modifiedB = new NativeList<byte>(B.Length + 2, Allocator.Persistent);

            // need two vectors of size 2 * MAX + 2
            int MAX = A.Length + B.Length + 1;
            // vector for the (0,0) to (x,y) search
            NativeList<int> DownVector = new NativeList<int>(2 * MAX + 2, Allocator.Persistent);
            // vector for the (u,v) to (N,M) search
            NativeList<int> UpVector = new NativeList<int>(2 * MAX + 2, Allocator.Persistent);

            // diff
            MyersDiffXBurst.DiffNonAlloc(A, B, modifiedA, modifiedB, DownVector, UpVector, result);
            Assert.That(result.Length, Is.EqualTo(1));
            AssertItem(result[0], 0, 0, 1, 1);

            // cleanup
            A.Dispose();
            B.Dispose();
            result.Dispose();
            modifiedA.Dispose();
            modifiedB.Dispose();
            DownVector.Dispose();
            UpVector.Dispose();
        }

        // first burst compiled test
        [Test]
        public void Diff_BurstCompiled()
        {
            // prepare a big byte[]
            NativeArray<byte> A = new NativeArray<byte>(3, Allocator.Persistent);
            NativeArray<byte> B = new NativeArray<byte>(3, Allocator.Persistent);
            // change one value in B
            B[0] = 0xFF;

            // prepare the caches for nonalloc
            // allocate the lists.
            // already with expected capacity to avoid resizing.
            NativeList<Item> result = new NativeList<Item>(1000, Allocator.Persistent);
            NativeList<byte> modifiedA = new NativeList<byte>(A.Length + 2, Allocator.Persistent);
            NativeList<byte> modifiedB = new NativeList<byte>(B.Length + 2, Allocator.Persistent);

            // need two vectors of size 2 * MAX + 2
            int MAX = A.Length + B.Length + 1;
            // vector for the (0,0) to (x,y) search
            NativeList<int> DownVector = new NativeList<int>(2 * MAX + 2, Allocator.Persistent);
            // vector for the (u,v) to (N,M) search
            NativeList<int> UpVector = new NativeList<int>(2 * MAX + 2, Allocator.Persistent);

            // diff - bursted version
            MyersDiffXBurst.DiffNonAlloc_Bursted(A, B, modifiedA, modifiedB, DownVector, UpVector, result);
            Assert.That(result.Length, Is.EqualTo(1));
            AssertItem(result[0], 0, 0, 1, 1);

            // cleanup
            A.Dispose();
            B.Dispose();
            result.Dispose();
            modifiedA.Dispose();
            modifiedB.Dispose();
            DownVector.Dispose();
            UpVector.Dispose();
        }

        [Test]
        public void Benchmark_1percent_changes_x1000()
        {
            // prepare a big byte[]
            NativeArray<byte> A = new NativeArray<byte>(1000, Allocator.Persistent);
            NativeArray<byte> B = new NativeArray<byte>(1000, Allocator.Persistent);
            // change 1/3rd of values in B
            for (int i = 0; i < B.Length; ++i)
                if (i % 10 == 0)
                    B[i] = 0xFF;

            // prepare the caches for nonalloc
            // allocate the lists.
            // already with expected capacity to avoid resizing.
            NativeList<Item> result = new NativeList<Item>(1000, Allocator.Persistent);
            NativeList<byte> modifiedA = new NativeList<byte>(A.Length + 2, Allocator.Persistent);
            NativeList<byte> modifiedB = new NativeList<byte>(B.Length + 2, Allocator.Persistent);

            // need two vectors of size 2 * MAX + 2
            int MAX = A.Length + B.Length + 1;
            // vector for the (0,0) to (x,y) search
            NativeList<int> DownVector = new NativeList<int>(2 * MAX + 2, Allocator.Persistent);
            // vector for the (u,v) to (N,M) search
            NativeList<int> UpVector = new NativeList<int>(2 * MAX + 2, Allocator.Persistent);

            // run 1k times
            for (int i = 0; i < 1000; ++i)
                MyersDiffXBurst.DiffNonAlloc(A, B, modifiedA, modifiedB, DownVector, UpVector, result);

            // cleanup
            A.Dispose();
            B.Dispose();
            result.Dispose();
            modifiedA.Dispose();
            modifiedB.Dispose();
            DownVector.Dispose();
            UpVector.Dispose();
        }

        [Test]
        public void Benchmark_10percent_changes_x1000()
        {
            // prepare a big byte[]
            NativeArray<byte> A = new NativeArray<byte>(1000, Allocator.Persistent);
            NativeArray<byte> B = new NativeArray<byte>(1000, Allocator.Persistent);
            // change 1/3rd of values in B
            for (int i = 0; i < B.Length; ++i)
                if (i % 10 == 0)
                    B[i] = 0xFF;

            // prepare the caches for nonalloc
            // allocate the lists.
            // already with expected capacity to avoid resizing.
            NativeList<Item> result = new NativeList<Item>(1000, Allocator.Persistent);
            NativeList<byte> modifiedA = new NativeList<byte>(A.Length + 2, Allocator.Persistent);
            NativeList<byte> modifiedB = new NativeList<byte>(B.Length + 2, Allocator.Persistent);

            // need two vectors of size 2 * MAX + 2
            int MAX = A.Length + B.Length + 1;
            // vector for the (0,0) to (x,y) search
            NativeList<int> DownVector = new NativeList<int>(2 * MAX + 2, Allocator.Persistent);
            // vector for the (u,v) to (N,M) search
            NativeList<int> UpVector = new NativeList<int>(2 * MAX + 2, Allocator.Persistent);

            // run 1k times
            for (int i = 0; i < 1000; ++i)
                MyersDiffXBurst.DiffNonAlloc(A, B, modifiedA, modifiedB, DownVector, UpVector, result);

            // cleanup
            A.Dispose();
            B.Dispose();
            result.Dispose();
            modifiedA.Dispose();
            modifiedB.Dispose();
            DownVector.Dispose();
            UpVector.Dispose();
        }

        [Test]
        public void Benchmark_30percent_changes_x1000()
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
            NativeList<byte> modifiedA = new NativeList<byte>(A.Length + 2, Allocator.Persistent);
            NativeList<byte> modifiedB = new NativeList<byte>(B.Length + 2, Allocator.Persistent);

            // need two vectors of size 2 * MAX + 2
            int MAX = A.Length + B.Length + 1;
            // vector for the (0,0) to (x,y) search
            NativeList<int> DownVector = new NativeList<int>(2 * MAX + 2, Allocator.Persistent);
            // vector for the (u,v) to (N,M) search
            NativeList<int> UpVector = new NativeList<int>(2 * MAX + 2, Allocator.Persistent);

            // run 1k times
            for (int i = 0; i < 1000; ++i)
                MyersDiffXBurst.DiffNonAlloc(A, B, modifiedA, modifiedB, DownVector, UpVector, result);

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
