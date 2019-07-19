// common code used by server and client
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Mirror.Tcp
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

        protected static int BytesToInt(byte[] bytes )
        {
            return
                bytes[0] |
                (bytes[1] << 8) |
                (bytes[2] << 16) |
                (bytes[3] << 24);

        }

        protected static void WriteSize(int length, byte[] bytes)
        {
            bytes[0] = (byte)length;
            bytes[1] = (byte)(length >> 8);
            bytes[2] = (byte)(length >> 16);
            bytes[3] = (byte)(length >> 24);
        }

        // send message (via stream) with the <size,content> message structure
        // throws exception if there is a problem
        protected static async Task SendMessage(NetworkStream stream, ArraySegment<byte> content)
        {
            // stream.Write throws exceptions if client sends with high
            // frequency and the server stops
           
            // construct header (size)

            // TODO:  we can do this without allocation
            // write header+content at once via payload array. writing
            // header,payload separately would cause 2 TCP packets to be
            // sent if nagle's algorithm is disabled(2x TCP header overhead)

            // TODO: what if we are sending this message to multiple clients?
            // we would allocate an identicall array buffer for all of them
            // should be possible to do just one and use it for all connections
            byte[] payload = new byte[4 + content.Count];
            WriteSize(content.Count, payload);
            Array.Copy(content.Array, content.Offset, payload, 4, content.Count);
            await stream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
        }

        // read message (via stream) with the <size,content> message structure
        protected static async Task<byte[]> ReadMessageAsync(Stream stream)
        {
            byte[] messageSizeBuffer = await stream.ReadExactlyAsync(4);

            if (messageSizeBuffer == null)
                return null; // end of stream,  just disconnect

            int messageSize = BytesToInt(messageSizeBuffer);

            return await stream.ReadExactlyAsync(messageSize);
        }

    }
}
