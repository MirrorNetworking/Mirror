using System;
using System.IO;

namespace Octodiff.Core
{
    public class BinaryDeltaWriter : IDeltaWriter
    {
        readonly BinaryWriter writer;

        public BinaryDeltaWriter(Stream stream)
        {
            writer = new BinaryWriter(stream);
        }

        public void WriteMetadata(byte[] expectedNewFileHash)
        {
            // SHA1 hash length is always 20 bytes
            //writer.WriteVarInt((ulong)expectedNewFileHash.Length);
            writer.Write(expectedNewFileHash);
        }

        public void WriteCopyCommand(DataRange segment)
        {
            //Console.WriteLine($"writing copy command offset={segment.StartOffset} length={segment.Length}");
            writer.Write(BinaryFormat.CopyCommand);
            writer.WriteVarInt((ulong)segment.StartOffset);
            writer.WriteVarInt((ulong)segment.Length);
        }

        public void WriteDataCommand(Stream source, long offset, long length)
        {
            //Console.WriteLine($"writing data command offset={offset} length={length}");
            writer.Write(BinaryFormat.DataCommand);
            writer.WriteVarInt((ulong)length);

            long originalPosition = source.Position;
            try
            {
                source.Seek(offset, SeekOrigin.Begin);

                byte[] buffer = new byte[Math.Min((int)length, 1024 * 1024)];

                int read;
                long soFar = 0;
                while ((read = source.Read(buffer, 0, (int)Math.Min(length - soFar, buffer.Length))) > 0)
                {
                    soFar += read;
                    writer.Write(buffer, 0, read);
                }
            }
            finally
            {
                source.Seek(originalPosition, SeekOrigin.Begin);
            }
        }

        public void Finish() {}
    }
}