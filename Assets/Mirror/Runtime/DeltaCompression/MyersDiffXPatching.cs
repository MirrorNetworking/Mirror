// helper class to convert MyersDiff result to binary patch.
// and to apply binary patch to another binary.
// this is framework dependent, so not part of MyersDiffX repo.
using System;
using System.Collections.Generic;
using MyersDiffX;

namespace Mirror
{
    public static class MyersDiffXPatching
    {
        // helper function to convert Diff.Item[] to an actual patch
        public static void MakePatch(ArraySegment<byte> A, ArraySegment<byte> B, List<Item> diffs, NetworkWriter result)
        {
            // serialize diffs
            //   deletedA means: it was in A, it's deleted in B.
            //   insertedB means: it wasn't in A, it's added to B.
            Compression.CompressVarInt(result, (ulong)diffs.Count);
            foreach (Item change in diffs)
            {
                // assuming the other end already has 'A'
                // we need to save instructions to construct 'B' from 'A'.

                // when applying the patch, we always apply it with VALUES from
                // 'A' to INDICES from 'B'. in other words, the other end never
                // needs 'StartA'.
                Compression.CompressVarInt(result, (ulong)change.StartB);

                // always need to know if / how many were deleted
                Compression.CompressVarInt(result, (ulong)change.deletedA);

                // always need to know how many were inserted
                Compression.CompressVarInt(result, (ulong)change.insertedB);

                // need to provide the actual values that were inserted
                // it means compared to 'A' at 'StartA',
                // 'B' at 'startB' has 'N' the following new values
                for (int i = 0; i < change.insertedB; ++i)
                {
                    // DO NOT _VARINT_ the actual value.
                    // it's just a byte. it could be anything. we don't know.
                    result.WriteByte(B.Array[change.StartB + i]);
                }
            }
        }

        public static void ApplyPatch(NetworkWriter A, NetworkReader delta, NetworkWriter result)
        {
            // TODO linked list for performance? insert is expensive
            // TODO avoid ToArray
            // convert A bytes to list for easier insertion/deletion
            List<byte> B = new List<byte>(A.ToArray());

            // deserialize patch
            int count = (int)Compression.DecompressVarInt(delta);
            // TODO safety..
            for (int i = 0; i < count; ++i)
            {
                // we only ever need (and serialize) StartB
                int StartB = (int)Compression.DecompressVarInt(delta);

                // deleted amount
                int deletedA = (int)Compression.DecompressVarInt(delta);

                // deletedA means: compared to A, 'N' were deleted in B at 'StartB'
                // TODO we need a linked list or similar data structure for perf
                B.RemoveRange(StartB, deletedA);

                // inserted amount
                int insertedB = (int)Compression.DecompressVarInt(delta);
                for (int n = 0; n < insertedB; ++n)
                {
                    // DO NOT _VARINT_ the actual value.
                    // it's just a byte. it could be anything. we don't know.
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
