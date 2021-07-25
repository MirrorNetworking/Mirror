using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Octodiff.Core
{
    public static class DeltaBuilder
    {
        const int ReadBufferSize = 4*1024*1024;

        // build delta with a signature data structure directly.
        // we don't need the binary stream signature in networked games.
        public static void BuildDelta(Stream newFileStream, List<ChunkSignature> chunks, IDeltaWriter deltaWriter)
        {
            HashAlgorithm hashAlgorithm = SHA1.Create();

            newFileStream.Seek(0, SeekOrigin.Begin);

            chunks = OrderChunksByChecksum(chunks);

            int minChunkSize;
            int maxChunkSize;
            Dictionary<uint, int> chunkMap = CreateChunkMap(chunks, out maxChunkSize, out minChunkSize);

            byte[] buffer = new byte[ReadBufferSize];
            long lastMatchPosition = 0;

            long fileSize = newFileStream.Length;
            //Console.WriteLine("Building delta", 0, fileSize);

            while (true)
            {
                long startPosition = newFileStream.Position;
                int read = newFileStream.Read(buffer, 0, buffer.Length);
                if (read < 0)
                    break;

                uint checksum = 0;

                int remainingPossibleChunkSize = maxChunkSize;

                for (int i = 0; i < read - minChunkSize + 1; i++)
                {
                    long readSoFar = startPosition + i;

                    int remainingBytes = read - i;
                    if (remainingBytes < maxChunkSize)
                    {
                        remainingPossibleChunkSize = minChunkSize;
                    }

                    if (i == 0 || remainingBytes < maxChunkSize)
                    {
                        checksum = Adler32RollingChecksumV2.Calculate(buffer, i, remainingPossibleChunkSize);
                    }
                    else
                    {
                        byte remove = buffer[i- 1];
                        byte add = buffer[i + remainingPossibleChunkSize - 1];
                        checksum = Adler32RollingChecksumV2.Rotate(checksum, remove, add, remainingPossibleChunkSize);
                    }

                    //Console.WriteLine("Building delta", readSoFar, fileSize);

                    if (readSoFar - (lastMatchPosition - remainingPossibleChunkSize) < remainingPossibleChunkSize)
                        continue;

                    if (!chunkMap.ContainsKey(checksum))
                        continue;

                    int startIndex = chunkMap[checksum];

                    for (int j = startIndex; j < chunks.Count && chunks[j].RollingChecksum == checksum; j++)
                    {
                        ChunkSignature chunk = chunks[j];

                        byte[] sha = hashAlgorithm.ComputeHash(buffer, i, remainingPossibleChunkSize);

                        if (StructuralComparisons.StructuralEqualityComparer.Equals(sha, chunks[j].Hash))
                        {
                            readSoFar += remainingPossibleChunkSize;

                            long missing = readSoFar - lastMatchPosition;
                            if (missing > remainingPossibleChunkSize)
                            {
                                deltaWriter.WriteDataCommand(newFileStream, lastMatchPosition, missing - remainingPossibleChunkSize);
                            }

                            deltaWriter.WriteCopyCommand(new DataRange(chunk.StartOffset, chunk.Length));
                            lastMatchPosition = readSoFar;
                            break;
                        }
                    }
                }

                if (read < buffer.Length)
                {
                    break;
                }

                newFileStream.Position = newFileStream.Position - maxChunkSize + 1;
            }

            if (newFileStream.Length != lastMatchPosition)
            {
                deltaWriter.WriteDataCommand(newFileStream, lastMatchPosition, newFileStream.Length - lastMatchPosition);
            }

            deltaWriter.Finish();
        }

        static List<ChunkSignature> OrderChunksByChecksum(List<ChunkSignature> chunks)
        {
            chunks.Sort(new ChunkSignatureChecksumComparer());
            return chunks;
        }

        static Dictionary<uint, int> CreateChunkMap(IList<ChunkSignature> chunks, out int maxChunkSize, out int minChunkSize)
        {
            //Console.WriteLine("Creating chunk map", 0, chunks.Count);
            maxChunkSize = 0;
            minChunkSize = int.MaxValue;

            Dictionary<uint, int> chunkMap = new Dictionary<uint, int>();
            for (int i = 0; i < chunks.Count; i++)
            {
                ChunkSignature chunk = chunks[i];
                if (chunk.Length > maxChunkSize) maxChunkSize = chunk.Length;
                if (chunk.Length < minChunkSize) minChunkSize = chunk.Length;

                if (!chunkMap.ContainsKey(chunk.RollingChecksum))
                {
                    chunkMap[chunk.RollingChecksum] = i;
                }

                //Console.WriteLine("Creating chunk map", i, chunks.Count);
            }
            return chunkMap;
        }
    }
}