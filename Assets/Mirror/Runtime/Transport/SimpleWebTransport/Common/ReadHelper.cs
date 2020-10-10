using System;
using System.IO;
using System.Threading;

namespace Mirror.SimpleWeb
{
    public static class ReadHelper
    {
        public enum ReadResult
        {
            Success = 1,
            ReadMinusOne = 2,
            ReadZero = 4,
            ReadLessThanLength = 8,
            Error = 16,
            Fail = ReadMinusOne | ReadZero | ReadLessThanLength | Error
        }
        public static ReadResult SafeRead(Stream stream, byte[] outBuffer, int outOffset, int length, bool checkLength = false)
        {
            try
            {
                int received = stream.Read(outBuffer, outOffset, length);

                if (received == -1)
                {
                    return ReadResult.ReadMinusOne;
                }

                if (received == 0)
                {
                    return ReadResult.ReadZero;
                }
                if (checkLength && received != length)
                {
                    return ReadResult.ReadLessThanLength;
                }

                return ReadResult.Success;
            }
            catch (AggregateException ae)
            {
                // if interupt is called we dont care about Exceptions
                CheckForInterupt();

                ae.Handle(e =>
                {
                    if (e is IOException io)
                    {
                        // this is only info as SafeRead is allowed to fail
                        Log.Info($"SafeRead IOException\n{io.Message}", false);
                        return true;
                    }

                    return false;
                });
                return ReadResult.Error;
            }
            catch (IOException e)
            {
                // if interupt is called we dont care about Exceptions
                CheckForInterupt();

                // this is only info as SafeRead is allowed to fail
                Log.Info($"SafeRead IOException\n{e.Message}", false);
                return ReadResult.Error;
            }
        }

        static void CheckForInterupt()
        {
            // sleep in order to check for ThreadInterruptedException
            Thread.Sleep(1);
        }

        public static int? SafeReadTillMatch(Stream stream, byte[] outBuffer, int outOffset, byte[] endOfHeader)
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
                Log.Info($"SafeRead IOException\n{e.Message}", false);
                return null;
            }
        }
    }
}
