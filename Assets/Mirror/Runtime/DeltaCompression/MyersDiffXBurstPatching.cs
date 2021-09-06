// helper class to convert MyersDiff result to binary patch.
// and to apply binary patch to another binary.
// this is framework dependent, so not part of MyersDiffX repo.
/*
using Mirror;
using Unity.Collections;

namespace MyersDiffX
{
    public static class MyersDiffXBurstPatching
    {
        // helper function to convert Diff.Item[] to an actual patch
        public static void MakePatch(NativeArray<byte> A, NativeArray<byte> B, NativeList<Item> diffs, NetworkWriter result)
        {
            // serialize diffs
            //   deletedA means: it was in A, it's deleted in B.
            //   insertedB means: it wasn't in A, it's added to B.
            Compression.CompressVarUInt(result, (ulong)diffs.Length);

            // for-int instead of foreach to avoid Enumerator performance.
            // it shows in profiler for heavy delta compression tests.
            for (int i = 0; i < diffs.Length; ++i)
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
                    result.WriteByte(B[change.StartB + c]);
                }
            }
        }

        public static void ApplyPatch(NetworkWriter A, NetworkReader delta, NetworkWriter result) =>
            MyersDiffXPatching.ApplyPatch(A, delta, result);
    }
}
*/
