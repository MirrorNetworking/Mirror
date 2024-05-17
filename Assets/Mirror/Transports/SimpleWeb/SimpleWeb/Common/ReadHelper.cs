using System;
using System.IO;
using System.Runtime.Serialization;

namespace Mirror.SimpleWeb
{
    public static class ReadHelper
    {
        /// <summary>
        /// Reads exactly length from stream
        /// </summary>
        /// <returns>outOffset + length</returns>
        /// <exception cref="ReadHelperException"></exception>
        public static int Read(Stream stream, byte[] outBuffer, int outOffset, int length)
        {
            int received = 0;
            try
            {
                while (received < length)
                {
                    int read = stream.Read(outBuffer, outOffset + received, length - received);
                    if (read == 0)
                        throw new ReadHelperException("returned 0");

                    received += read;
                }
            }
            catch (AggregateException ae)
            {
                // if interrupt is called we don't care about Exceptions
                Utils.CheckForInterupt();

                // rethrow
                ae.Handle(e => false);
            }

            if (received != length)
                throw new ReadHelperException("returned not equal to length");

            return outOffset + received;
        }

        /// <summary>
        /// Reads and returns results. This should never throw an exception
        /// </summary>
        public static bool TryRead(Stream stream, byte[] outBuffer, int outOffset, int length)
        {
            try
            {
                Read(stream, outBuffer, outOffset, length);
                return true;
            }
            catch (ReadHelperException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
            catch (Exception e)
            {
                Log.Exception(e);
                return false;
            }
        }

        public static int? SafeReadTillMatch(Stream stream, byte[] outBuffer, int outOffset, int maxLength, byte[] endOfHeader)
        {
            try
            {
                int read = 0;
                int endIndex = 0;
                int endLength = endOfHeader.Length;
                while (true)
                {
                    int next = stream.ReadByte();
                    if (next == -1) // closed
                        return null;

                    if (read >= maxLength)
                    {
                        Log.Error("[SWT-ReadHelper]: SafeReadTillMatch exceeded maxLength");
                        return null;
                    }

                    outBuffer[outOffset + read] = (byte)next;
                    read++;

                    // if n is match, check n+1 next
                    if (endOfHeader[endIndex] == next)
                    {
                        endIndex++;
                        // when all is match return with read length
                        if (endIndex >= endLength)
                            return read;
                    }
                    // if n not match reset to 0
                    else
                        endIndex = 0;
                }
            }
            catch (IOException e)
            {
                Log.InfoException(e);
                return null;
            }
            catch (Exception e)
            {
                Log.Exception(e);
                return null;
            }
        }
    }

    [Serializable]
    public class ReadHelperException : Exception
    {
        public ReadHelperException(string message) : base(message) { }

        protected ReadHelperException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
