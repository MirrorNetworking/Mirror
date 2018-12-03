// common code used by server and client
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Telepathy
{
    public abstract class Common
    {
    
        // static helper functions /////////////////////////////////////////////
        // fast int to byte[] conversion and vice versa
        // -> test with 100k conversions:
        //    BitConverter.GetBytes(ushort): 144ms
        //    bit shifting: 11ms
        // -> 10x speed improvement makes this optimization actually worth it
        // -> this way we don't need to allocate BinaryWriter/Reader either
        // -> 4 bytes because some people may want to send messages larger than
        //    64K bytes
        protected static byte[] IntToBytes(int value)
        {
            return new byte[] {
                (byte)value,
                (byte)(value >> 8),
                (byte)(value >> 16),
                (byte)(value >> 24)
            };
        }

        protected static int BytesToInt(byte[] bytes )
        {
            return
                bytes[0] |
                (bytes[1] << 8) |
                (bytes[2] << 16) |
                (bytes[3] << 24);

        }

        // send message (via stream) with the <size,content> message structure
        protected static bool SendMessage(NetworkStream stream, byte[] content)
        {
            // can we still write to this socket (not disconnected?)
            if (!stream.CanWrite)
            {
                Logger.LogWarning("Send: stream not writeable: " + stream);
                return false;
            }

            // stream.Write throws exceptions if client sends with high
            // frequency and the server stops
            try
            {
                // construct header (size)
                byte[] header = IntToBytes(content.Length);

                // write header+content at once via payload array. writing
                // header,payload separately would cause 2 TCP packets to be
                // sent if nagle's algorithm is disabled(2x TCP header overhead)
                byte[] payload = new byte[header.Length + content.Length];
                Array.Copy(header, payload, header.Length);
                Array.Copy(content, 0, payload, header.Length, content.Length);
                stream.Write(payload, 0, payload.Length);

                return true;
            }
            catch (Exception exception)
            {
                // log as regular message because servers do shut down sometimes
                Logger.Log("Send: stream.Write exception: " + exception);
                return false;
            }
        }

        // read message (via stream) with the <size,content> message structure
        protected static async Task<byte[]> ReadMessageAsync(NetworkStream stream)
        {
            byte[] messageSizeBuffer = await stream.ReadExactlyAsync(4);

            if (messageSizeBuffer == null)
                return null; // end of stream,  just disconnect

            int messageSize = BytesToInt(messageSizeBuffer);

            return await stream.ReadExactlyAsync(messageSize);
        }

    }
}
