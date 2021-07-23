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
                // ApplyPatch 'from scratch' version needs StartA, NOT StartB.
                Compression.CompressVarInt(result, (ulong)change.StartA);
                // ApplyPatch 'duplicate & apply' version needs StartB, not StartA.
                //Compression.CompressVarInt(result, (ulong)change.StartB);
                Compression.CompressVarInt(result, (ulong)change.deletedA);
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
            // the challenge here is to reconstruct B := A + Delta
            // AND do that without allocations.
            //
            // the easy solution is to duplicate A and apply all changes.
            // that's too slow though. need RemoveRange/Insert/duplications etc.
            //
            // let's try to reconstruct from scratch, directly into the result.
            //
            // for reference, here is a simple example:
            //   Delta(abc, aab) gives:
            //     item: startA=1 startB=1 deletedA=0 insertedB=1
            //     item: startA=2 startB=3 deletedA=1 insertedB=0
            //
            // applying a patch FORWARD, step by step:
            //     B := A
            //     B = abc
            //     we insert 1 value from A[StartA] at B[StartB]:
            //     B = aabc
            //     we delete 1 value that was at A[StartA] in B[StartB]:
            //     B = aab
            //
            // applying a patch FROM SCRATCH, step by step:
            //     B = ""
            //     first change is insert 1 value from A[StartA] at B[StartB]:
            //       copy A until StartA first:
            //         B = a
            //       insert the value from A[StartA] now:
            //         B = aa
            //     second change is delete 1 value from A[StartA] at B[StartB]:
            //       copy A until StartA first, from where we left of
            //         B = aab
            //       delete the value from A[StartA] now:
            //         means simply skip them in A
            //
            // Benchmark for 100k ApplyPatch calls:
            //   duplicate A & apply:      395ms
            //   reconstruct from scratch: 146ms (nonalloc!)

            ArraySegment<byte> ASegment = A.ToArraySegment();
            int indexA = 0;

            // reconstruct...
            int count = (int)Compression.DecompressVarInt(delta);
            for (int i = 0; i < count; ++i)
            {
                // read the next change
                int StartA = (int)Compression.DecompressVarInt(delta);
                //int StartB = (int)Compression.DecompressVarInt(delta);
                int deletedA = (int)Compression.DecompressVarInt(delta);
                int insertedB = (int)Compression.DecompressVarInt(delta);

                // we progressed through 'A' until 'IndexA'.
                // copy everything until the next change at 'StartB'

                // first of: copy everything from A[indexA] until this change
                //           EXCLUDING this change/index
                int copy = StartA - indexA;
                result.WriteBytes(ASegment.Array, ASegment.Offset + indexA, copy);
                indexA += copy;

                // deletedA means we don't take those from A.
                // in other words, skip them.
                // TODO safety. should be > 0 and within range etc.
                indexA += deletedA;

                // inserted means we have 'N' new values in delta.
                for (int n = 0; n < insertedB; ++n)
                {
                    // DO NOT _VARINT_ the actual value.
                    // it's just a byte. it could be anything. we don't know.
                    byte value = delta.ReadByte();
                    result.WriteByte(value);
                    //Debug.Log($"->patch: inserted '0x{value:X2}' into B @ {StartB + n} => {BitConverter.ToString(B.ToArray())}");
                }
            }

            // we may have applied changes until indexA.
            // copy everything that's left.
            int remainder = ASegment.Count - indexA;
            result.WriteBytes(ASegment.Array, ASegment.Offset + indexA, remainder);



            // FOR FUTURE REFERENCE, this is the 'DUPLICATE A & RECONSTRUCT'
            // algorithm.
            //
            // DUPLICATE A needs to always send 'StartB', NOT 'StartA'.
            // FROM SCRATCH always needs to send 'StartA', NOT 'StartB'.
            /*
            List<byte> B = new List<byte>();
            ArraySegment<byte> ASegment = A.ToArraySegment();
            for (int i = 0; i < ASegment.Count; ++i)
                B.Add(ASegment.Array[ASegment.Offset + i]);

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

            // put B into result writer (nonalloc)
            for (int i = 0; i < B.Count; ++i)
                result.WriteByte(B[i]);
            */
        }
    }
}
