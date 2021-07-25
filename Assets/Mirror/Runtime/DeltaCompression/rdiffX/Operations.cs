// high level convenience functions
using System.Collections.Generic;
using System.IO;

namespace Octodiff.Core
{
    public static class Operations
    {
        // helper function to create signature from byte[]
        public static void CreateSignature(byte[] A, short chunkSize, List<ChunkSignature> result)
        {
            // create signature
            using (MemoryStream basisStream = new MemoryStream(A))
            {
                SignatureBuilder.BuildSignature(basisStream, chunkSize, result);
            }
        }

        // helper function to create delta
        public static byte[] CreateDelta(byte[] A, List<ChunkSignature> signature, byte[] B)
        {
            using (MemoryStream newFileStream = new MemoryStream(B))
            using (MemoryStream deltaStream = new MemoryStream())
            {
                DeltaBuilder.BuildDelta(newFileStream,
                    signature,
                    new AggregateCopyOperationsDecorator(new BinaryDeltaWriter(deltaStream))
                );
                return deltaStream.ToArray();
            }
        }

        public static byte[] Patch(byte[] A, byte[] delta)
        {
            using (MemoryStream basisStream = new MemoryStream(A))
            using (MemoryStream deltaStream = new MemoryStream(delta))
            using (MemoryStream newFileStream = new MemoryStream())
            {
                DeltaApplier.Apply(basisStream, new BinaryDeltaReader(deltaStream), newFileStream);
                return newFileStream.ToArray();
            }
        }
    }
}