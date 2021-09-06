// helper class to convert MyersDiff result to binary patch.
// and to apply binary patch to another binary.
// this is framework dependent, so not part of MyersDiffX repo.
using System;
using System.Collections.Generic;
using Mirror;

namespace MyersDiffX
{
    public static class MyersDiffXPatching
    {
        // helper function to convert Diff.Item[] to an actual patch
        public static void MakePatch(ArraySegment<byte> A, ArraySegment<byte> B, List<Item> diffs, NetworkWriter result)
        {
            // serialize diffs
            //   deletedA means: it was in A, it's deleted in B.
            //   insertedB means: it wasn't in A, it's added to B.
            Compression.CompressVarUInt(result, (ulong)diffs.Count);

            // for-int instead of foreach to avoid Enumerator performance.
            // it shows in profiler for heavy delta compression tests.
            for (int i = 0; i < diffs.Count; ++i)
            {
                Item change = diffs[i];

                // ApplyPatch 'from scratch' version needs StartA, NOT StartB.
                Compression.CompressVarUInt(result, (ulong)change.StartA);
                // ApplyPatch 'duplicate & apply' version needs StartB, not StartA.
                //Compression.CompressVarUInt(result, (ulong)change.StartB);
                Compression.CompressVarUInt(result, (ulong)change.deletedA);
                Compression.CompressVarUInt(result, (ulong)change.insertedB);

                // need to provide the actual values that were inserted
                // it means compared to 'A' at 'StartA',
                // 'B' at 'startB' has 'N' the following new values
                for (int c = 0; c < change.insertedB; ++c)
                {
                    // DO NOT _VARINT_ the actual value.
                    // it's just a byte. it could be anything. we don't know.
                    result.WriteByte(B.Array[change.StartB + c]);
                }
            }
        }

        // ApplyPatch 'from scratch' version.
        // * starts with empty writer
        // * for each change:
        //   * copies everything until next change
        //   * applies change (delete=skip, insert=insert)
        // * copies remainder at the end
        //
        // harder to think about, but faster than the 'duplicate & apply' version
        //
        // Benchmark for 100k ApplyPatch calls:
        //   duplicate A & apply:      395ms
        //   reconstruct from scratch: 146ms (nonalloc!)
        public static void ApplyPatch(NetworkWriter A, NetworkReader delta, NetworkWriter result)
        {
            ArraySegment<byte> ASegment = A.ToArraySegment();
            int indexA = 0;

            // reconstruct...
            int count = (int)Compression.DecompressVarUInt(delta);
            for (int i = 0; i < count; ++i)
            {
                // read the next change
                int StartA = (int)Compression.DecompressVarUInt(delta);
                //int StartB = (int)Compression.DecompressVarUInt(delta);
                int deletedA = (int)Compression.DecompressVarUInt(delta);
                int insertedB = (int)Compression.DecompressVarUInt(delta);

                // we progressed through A until indexA already.
                // now we have change at StartA.
                // copy everything from indexA until before that change first.
                // TODO safety. should be > 0 and within range etc.
                int copy = StartA - indexA;
                result.WriteBytes(ASegment.Array, ASegment.Offset + indexA, copy);
                indexA += copy;

                // deletedA means we don't take those from A.
                // in other words, skip them.
                // TODO safety. should be > 0 and within range etc.
                indexA += deletedA;

                // inserted means we have 'N' new values in delta.
                // DO NOT _VARINT_ the actual values
                // it's just a byte. it could be anything. we don't know.
                ArraySegment<byte> inserted = delta.ReadBytesSegment(insertedB);
                result.WriteBytes(inserted.Array, inserted.Offset, inserted.Count);
            }

            // we may have applied changes until indexA.
            // copy everything that's left.
            int remainder = ASegment.Count - indexA;
            result.WriteBytes(ASegment.Array, ASegment.Offset + indexA, remainder);
        }

        /*
        // ApplyPatch 'duplicate & apply' version:
        // * duplicates A into B
        // * applies all 'delta' changes in order
        //
        // simple, but slow because:
        // - we need to duplicate A into a List<byte>
        // - we need to allocate that list
        // - we need to Insert & Delete, which is slow in lists
        public static void ApplyPatch(NetworkWriter A, NetworkReader delta, NetworkWriter result)
        {
            List<byte> B = new List<byte>();
            ArraySegment<byte> ASegment = A.ToArraySegment();
            for (int i = 0; i < ASegment.Count; ++i)
                B.Add(ASegment.Array[ASegment.Offset + i]);

            // deserialize patch
            int count = (int)Compression.DecompressVarUInt(delta);
            // TODO safety..
            for (int i = 0; i < count; ++i)
            {
                // we only ever need (and serialize) StartB
                int StartB = (int)Compression.DecompressVarUInt(delta);

                // deleted amount
                int deletedA = (int)Compression.DecompressVarUInt(delta);

                // deletedA means: compared to A, 'N' were deleted in B at 'StartB'
                // TODO we need a linked list or similar data structure for perf
                B.RemoveRange(StartB, deletedA);

                // inserted amount
                int insertedB = (int)Compression.DecompressVarUInt(delta);
                for (int n = 0; n < insertedB; ++n)
                {
                    // DO NOT _VARINT_ the actual value.
                    // it's just a byte. it could be anything. we don't know.
                    byte value = delta.ReadByte();
                    B.Insert(StartB + n, value);
                    //Debug.Log($"->patch: inserted '0x{value:X2}' into B @ {StartB + n} => {BitConverter.ToString(B.ToArray())}");
                }
            }

            // put B into result writer (nonalloc)
            for (int i = 0; i < B.Count; ++i)
                result.WriteByte(B[i]);
        }
        */
    }
}
