// original paper: http://www.xmailserver.org/diff2.pdf
// used in diff, git!
//
// VARINT before/after:
//   Delta_Tiny:   17 bytes =>  5 bytes
//   Delta_Small:  83 bytes => 26 bytes
//   Delta_Big:   318 bytes => 99 bytes
using System.Collections.Generic;
using MyersDiff;

namespace Mirror.Tests.DeltaCompression
{
    public class MyersDiffTests : DeltaCompressionTests
    {
        // helper function to convert Diff.Item[] to an actual patch
        public static void MakePatch(int[] A, int[] B, Diff.Item[] diffs, NetworkWriter result)
        {
            // serialize diffs
            //   deletedA means: it was in A, it's deleted in B.
            //   insertedB means: it wasn't in A, it's added to B.
            VarInt.WriteULong(result, (ulong)diffs.Length);
            foreach (Diff.Item change in diffs)
            {
                // assuming the other end already has 'A'
                // we need to save instructions to construct 'B' from 'A'.

                // when applying the patch, we always apply it with VALUES from
                // 'A' to INDICES from 'B'. in other words, the other end never
                // needs 'StartA'.
                VarInt.WriteULong(result, (ulong)change.StartB);

                // always need to know if / how many were deleted
                VarInt.WriteULong(result, (ulong)change.deletedA);

                // always need to know how many were inserted
                VarInt.WriteULong(result, (ulong)change.insertedB);

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
                UnityEngine.Debug.Log($"item: startA={item.StartA} startB={item.StartB} deletedA={item.deletedA} insertedB={item.insertedB}");

            // make patch
            MakePatch(fromInts, toInts, diffs, result);
        }

        public override void ApplyPatch(NetworkWriter A, NetworkReader delta, NetworkWriter result)
        {
            // convert A bytes to list for easier insertion/deletion
            List<byte> B = new List<byte>(A.ToArray());

            // deserialize patch
            int count = (int)VarInt.ReadULong(delta);
            // TODO safety..
            for (int i = 0; i < count; ++i)
            {
                // we only ever need (and serialize) StartB
                int StartB = (int)VarInt.ReadULong(delta);

                // deleted amount
                int deletedA = (int)VarInt.ReadULong(delta);

                // deletedA means: compared to A, 'N' were deleted in B at 'StartB'
                // TODO we need a linked list or similar data structure for perf
                B.RemoveRange(StartB, deletedA);

                // inserted amount
                int insertedB = (int)VarInt.ReadULong(delta);
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
    }
}
