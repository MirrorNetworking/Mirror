using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Octodiff.Core
{
    public static class SignatureBuilder
    {
        // minimum was originally 128 bytes for no obvious reason.
        public const short MinimumChunkSize = 32;
        public const short DefaultChunkSize = 2048;
        public const short MaximumChunkSize = 31 * 1024;

        // validate chunk size helper function
        static void ValidateChunkSize(int chunkSize)
        {
            // validate chunk size
            if (chunkSize < MinimumChunkSize)
                throw new Exception($"Chunk size cannot be less than {MinimumChunkSize}");
            if (chunkSize > MaximumChunkSize)
                throw new Exception($"Chunk size cannot be exceed {MaximumChunkSize}");
        }

        // builds signature into data structure.
        // skips the binary stream part.
        // we don't need it for networked games.
        //
        // result as parameter to support nonalloc
        public static void BuildSignature(Stream stream, short chunkSize, List<ChunkSignature> result)
        {
            ValidateChunkSize(chunkSize);

            HashAlgorithm hashAlgorithm = SHA1.Create();
            GatherChunkSignatures(stream, chunkSize, hashAlgorithm, result);
        }

        // like WriteChunkSignatures but returns them directly instead of writing
        static void GatherChunkSignatures(
            Stream stream,
            short chunkSize,
            HashAlgorithm hashAlgorithm,
            List<ChunkSignature> result)
        {
            result.Clear();

            //Console.WriteLine("Building signatures", 0, stream.Length);
            stream.Seek(0, SeekOrigin.Begin);

            long start = 0;
            int read;
            byte[] block = new byte[chunkSize];
            while ((read = stream.Read(block, 0, block.Length)) > 0)
            {
                result.Add(new ChunkSignature
                {
                    StartOffset = start,
                    Length = (short)read,
                    Hash = hashAlgorithm.ComputeHash(block, 0, read),
                    RollingChecksum = Adler32RollingChecksumV2.Calculate(block, 0, read)
                });

                start += read;
                //Console.WriteLine("Building signature", start, stream.Length);
            }
        }
    }
}