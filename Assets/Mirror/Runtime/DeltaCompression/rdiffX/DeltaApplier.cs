using System;
using System.IO;

namespace Octodiff.Core
{
    public static class DeltaApplier
    {
        public static void Apply(Stream basisFileStream, BinaryDeltaReader delta, Stream outputStream)
        {
            delta.Apply(
                writeData: (data) => outputStream.Write(data, 0, data.Length),
                copy: (startPosition, length) =>
                {
                    basisFileStream.Seek(startPosition, SeekOrigin.Begin);

                    byte[] buffer = new byte[4*1024*1024];
                    int read;
                    long soFar = 0;
                    while ((read = basisFileStream.Read(buffer, 0, (int)Math.Min(length - soFar, buffer.Length))) > 0)
                    {
                        soFar += read;
                        outputStream.Write(buffer, 0, read);
                    }
                });
        }
    }
}