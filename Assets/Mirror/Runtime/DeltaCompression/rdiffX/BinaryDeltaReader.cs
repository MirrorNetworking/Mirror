using System;
using System.IO;

namespace Octodiff.Core
{
    public class BinaryDeltaReader
    {
        readonly BinaryReader reader;
        bool hasReadMetadata;

        public BinaryDeltaReader(Stream stream)
        {
            reader = new BinaryReader(stream);
        }

        // TODO remove? can Apply always Seek(0) or only once?
        void EnsureMetadata()
        {
            if (hasReadMetadata)
                return;

            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            hasReadMetadata = true;
        }

        public void Apply(Action<byte[]> writeData, Action<long, long> copy)
        {
            long fileLength = reader.BaseStream.Length;

            EnsureMetadata();

            while (reader.BaseStream.Position != fileLength)
            {
                byte b = reader.ReadByte();

                if (b == BinaryFormat.CopyCommand)
                {
                    long start = (long)reader.ReadVarInt();
                    long length = (long)reader.ReadVarInt();
                    copy(start, length);
                }
                else if (b == BinaryFormat.DataCommand)
                {
                    long length = (long)reader.ReadVarInt();
                    long soFar = 0;
                    while (soFar < length)
                    {
                        byte[] bytes = reader.ReadBytes((int) Math.Min(length - soFar, 1024*1024*4));
                        soFar += bytes.Length;
                        writeData(bytes);
                    }
                }
            }
        }
    }
}