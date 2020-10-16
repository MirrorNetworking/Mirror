using System;
using System.IO;
using System.Runtime.Serialization;

namespace Mirror.SimpleWeb
{
    public static class ReadHelper
    {
        /// <returns>outOffset + length</returns>
        /// <exception cref="ReadHelperException"></exception>
        public static int Read(Stream stream, byte[] outBuffer, int outOffset, int length)
        {
            int received = 0;
            try
            {
                received = stream.Read(outBuffer, outOffset, length);
            }
            catch (AggregateException ae)
            {
                // if interupt is called we dont care about Exceptions
                Utils.CheckForInterupt();

                // rethrow
                ae.Handle(e => false);
            }

            if (received == -1)
            {
                throw new ReadHelperException("returned -1");
            }

            if (received == 0)
            {
                throw new ReadHelperException("returned 0");
            }
            if (received != length)
            {
                throw new ReadHelperException("returned not equal to length");
            }

            return outOffset + received;
        }

        /// <summary>
        /// Reads and returns results. This should never throw an exception
        /// </summary>
        public static bool TryRead(Stream stream, byte[] outBuffer, int outOffset, int length)
        {
            try
            {
                int count = Read(stream, outBuffer, outOffset, length);
                return count == length;
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
                        Log.Error("SafeReadTillMatch exceeded maxLength");
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
                        {
                            return read;
                        }
                    }
                    // if n not match reset to 0
                    else
                    {
                        endIndex = 0;
                    }
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

        protected ReadHelperException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
