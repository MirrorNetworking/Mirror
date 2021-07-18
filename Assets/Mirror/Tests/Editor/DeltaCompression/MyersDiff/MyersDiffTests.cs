// original paper: http://www.xmailserver.org/diff2.pdf
// used in diff, git!
using System;
using System.Collections.Generic;
using MyersDiff;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.DeltaCompression
{
    public class MyersDiffTests : DeltaCompressionTests
    {
        // helper function to convert Diff.Item[] to an actual patch
        public static void MakePatch(int[] A, int[] B, Diff.Item[] diffs, NetworkWriter result)
        {
            // an item has:
            //   StartA
            //   StartB
            //   DeletedA
            //   InsertedB
            //
            // to make an actual patch, we need to also included the insered values.
            // (the deleted values are deleted. we don't need to include those.)
            /*List<Modified> result = new List<Modified>();
            foreach (Diff.Item item in diffs)
            {
                Modified modified = new Modified();
                modified.indexA = item.StartA;
                modified.indexB = item.StartB;
                modified.deletedA = item.deletedA;
                // add the inserted values
                modified.insertedB = new List<byte>();
                for (int i = 0; i < item.insertedB; ++i)
                {
                    // TODO pass byte[] to begin with.
                    Debug.Log($"->inserting @ A={item.StartA} value={A[item.StartA]}");
                    modified.insertedB.Add((byte)A[item.StartA]);
                }
                result.Add(modified);
            }
            return result;*/


            // serialize diffs
            //   deletedA means: it was in A, it's deleted in B.
            //   insertedB means: it wasn't in A, it's added to B.
            // TODO varint
            result.WriteInt(diffs.Length);
            foreach (Diff.Item change in diffs)
            {
                // assuming the other end already has 'A'
                // we need to save instructions to construct 'B' from 'A'.

                // when applying the patch, we always apply it with VALUES from
                // 'A' to INDICES from 'B'. in other words, the other end never
                // needs 'StartA'.
                // TODO varint
                result.WriteInt(change.StartB);

                // always need to know if / how many were deleted
                result.WriteInt(change.deletedA);

                // always need to know how many were inserted
                result.WriteInt(change.insertedB);

                // need to provide the actual values that were inserted
                // it means compared to 'A' at 'StartA',
                // 'B' at 'startB' has 'N' the following new values
                for (int i = 0; i < change.insertedB; ++i)
                {
                    // TODO use byte to begin with instead of int[]. or <T>.
                    result.WriteByte((byte)B[change.StartB + i]);
                }
            }
        }

        public override void ComputeDelta(NetworkWriter from, NetworkWriter to, NetworkWriter result)
        {
            // algorithm needs int[] for now
            // TODO use byte[]
            byte[] fromBytes = from.ToArray();
            int[] fromInts = new int[fromBytes.Length];
            for (int i = 0; i < fromBytes.Length; ++i)
                fromInts[i] = fromBytes[i];

            byte[] toBytes = to.ToArray();
            int[] toInts = new int[toBytes.Length];
            for (int i = 0; i < toBytes.Length; ++i)
                toInts[i] = toBytes[i];

            // myers diff
            Diff.Item[] diffs = Diff.DiffInt(fromInts, toInts);
            foreach (Diff.Item item in diffs)
                Debug.Log($"item: startA={item.StartA} startB={item.StartB} deletedA={item.deletedA} insertedB={item.insertedB}");

            // make patch
            MakePatch(fromInts, toInts, diffs, result);
        }

        public override void ApplyPatch(NetworkWriter A, NetworkReader delta, NetworkWriter result)
        {
            // convert A bytes to list for easier insertion/deletion
            List<byte> B = new List<byte>(A.ToArray());

            // deserialize patch
            int count = delta.ReadInt();
            // TODO safety..
            for (int i = 0; i < count; ++i)
            {
                // we only ever need (and serialize) StartB
                int StartB = delta.ReadInt();

                // deleted amount
                int deletedA = delta.ReadInt();

                // deletedA means: compared to A, these were deleted in B.
                // TODO we need a linked list or similar data structure for perf
                for (int n = 0; n < deletedA; ++n)
                {
                    // remove at 'StartB'.
                    // DO NOT remove at 'StartB + n'.
                    // every removal moves the end of the list to 'StartB' again.
                    B.RemoveAt(StartB);
                    //Debug.Log($"->patch: removed from B @ StartB={StartB} + n={n} => {BitConverter.ToString(B.ToArray())}");
                }

                // inserted amount
                int insertedB = delta.ReadInt();
                for (int n = 0; n < insertedB; ++n)
                {
                    byte value = delta.ReadByte();
                    B.Insert(StartB + n, value);
                    //Debug.Log($"->patch: inserted '0x{value:X2}' into B @ {StartB + n} => {BitConverter.ToString(B.ToArray())}");
                }
            }

            // convert to byte[]
            result.WriteBytes(B.ToArray(), 0, B.Count);
        }

        // simple test for understanding
        /*[Test]
        public void SimpleTest()
        {
            // test values larger than indices for easier reading
            // -> we want soething like ABCBCDE so we have a reptition of
            //    different values in there like BCBC
            // -> this way we can test what 'insertedB' means
            int[] A = {11, 22, 33, 22, 33,         44, 55};
            int[] B = {11, 22, 33, 22, 33, 22, 33, 44};
            Debug.Log($"A={String.Join(", ", A)}");
            Debug.Log($"B={String.Join(", ", B)}");

            // myers diff
            Diff.Item[] items = Diff.DiffInt(A, B);
            foreach (Diff.Item item in items)
                Debug.Log($"item: startA={item.StartA} startB={item.StartB} deletedA={item.deletedA} insertedB={item.insertedB}");
        }*/
    }
}
