using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Mirror.Transport.Tcp
{

    public static class NetworkStreamExtensions
    {
        // helper function to read EXACTLY 'n' bytes
        // -> default .Read reads up to 'n' bytes. this function reads exactly 'n'
        //    bytes
        // -> either return all the bytes requested or null if end of stream
        public static async Task<byte[]> ReadExactlyAsync(this Stream stream, int size)
        {
            byte[] data = new byte[size];

            int offset = 0;

            // keep reading until we fill up the buffer;
            while (offset < size)
            {
                int received = 0;

                if (stream is NetworkStream && ((NetworkStream)stream).DataAvailable)
                {
                    // read available data immediatelly
                    // this is an important optimization because unity seems
                    // to wait until the next frame every time we call ReadAsync
                    // so if we have a bunch of data waiting in the buffer it takes a long
                    // time to receive it.
                    received = stream.Read(data, offset, size - offset);
                }
                else
                {
                    // wait for more data
                    received = await stream.ReadAsync(data, offset, size - offset);
                }

                // we just got disconnected
                if (received == 0)
                {
                    return null;
                }

                offset += received;
            }

            return data;
        }

    }
}