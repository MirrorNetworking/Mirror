// ---------------------------------------------------------------------
// Copyright 2018 David Haig
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// ---------------------------------------------------------------------

using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Ninja.WebSockets.Internal
{
    internal class BinaryReaderWriter
    {
        public static async Task ReadExactly(int length, Stream stream, ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            if (buffer.Count < length)
            {
                // This will happen if the calling function supplied a buffer that was too small to fit the payload of the websocket frame.
                // Note that this can happen on the close handshake where the message size can be larger than the regular payload
                throw new InternalBufferOverflowException($"Unable to read {length} bytes into buffer (offset: {buffer.Offset} size: {buffer.Count}). Use a larger read buffer");
            }

            int offset = 0;
            while (offset < length)
            {
                int bytesRead = 0;

                NetworkStream networkStream = stream as NetworkStream;
                if (networkStream != null && networkStream.DataAvailable)
                {
                    // paul: if data is available read it immediatelly.
                    // in my tests this performed a lot better,  because ReadAsync always waited until
                    // the next frame.
                    bytesRead = stream.Read(buffer.Array, buffer.Offset + offset, length - offset);
                }
                else
                {
                    bytesRead = await stream.ReadAsync(buffer.Array, buffer.Offset + offset, length - offset, cancellationToken);
                }

                if (bytesRead == 0)
                {
                    throw new EndOfStreamException(string.Format("Unexpected end of stream encountered whilst attempting to read {0:#,##0} bytes", length));
                }

                offset += bytesRead;
            }
        }

        public static async Task<ushort> ReadUShortExactly(Stream stream, bool isLittleEndian, ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            await ReadExactly(2, stream, buffer, cancellationToken);

            if (!isLittleEndian)
            {
                // big endian
                Array.Reverse(buffer.Array, buffer.Offset, 2);
            }

            return BitConverter.ToUInt16(buffer.Array, buffer.Offset);
        }

        public static async Task<ulong> ReadULongExactly(Stream stream, bool isLittleEndian, ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            await ReadExactly(8, stream, buffer, cancellationToken);

            if (!isLittleEndian)
            {
                // big endian
                Array.Reverse(buffer.Array, buffer.Offset, 8);
            }

            return BitConverter.ToUInt64(buffer.Array, buffer.Offset);
        }

        public static async Task<long> ReadLongExactly(Stream stream, bool isLittleEndian, ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            await ReadExactly(8, stream, buffer, cancellationToken);

            if (!isLittleEndian)
            {
                // big endian
                Array.Reverse(buffer.Array, buffer.Offset, 8);
            }

            return BitConverter.ToInt64(buffer.Array, buffer.Offset);
        }

        public static void WriteInt(int value, Stream stream, bool isLittleEndian)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian && !isLittleEndian)
            {
                Array.Reverse(buffer);
            }

            stream.Write(buffer, 0, buffer.Length);
        }

        public static void WriteULong(ulong value, Stream stream, bool isLittleEndian)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian && !isLittleEndian)
            {
                Array.Reverse(buffer);
            }

            stream.Write(buffer, 0, buffer.Length);
        }

        public static void WriteLong(long value, Stream stream, bool isLittleEndian)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian && !isLittleEndian)
            {
                Array.Reverse(buffer);
            }

            stream.Write(buffer, 0, buffer.Length);
        }

        public static void WriteUShort(ushort value, Stream stream, bool isLittleEndian)
        {
            byte[] buffer = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian && !isLittleEndian)
            {
                Array.Reverse(buffer);
            }

            stream.Write(buffer, 0, buffer.Length);
        }
    }
}
