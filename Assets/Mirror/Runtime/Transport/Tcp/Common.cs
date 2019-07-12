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
        protected static async Task SendMessage(NetworkStream stream, byte[] payload)
        {
            // note that payload has a 4 byte message length prefix from mirror
            await stream.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
        }

        // read message (via stream) with the <size,content> message structure
        protected static async Task<bool> ReadMessageAsync(Stream stream, MemoryStream buffer)
        {

            // messages are packed with a 4 byte in message size
            // read the size to see how much more data we need
            int headerSize = await stream.ReadExactlyAsync(4, buffer);

            if (headerSize == 0)
                return false; // end of stream,  just disconnect

            
            stream.Position -= headerSize;

            int messageSize =
                stream.ReadByte() |
                (stream.ReadByte() << 8) |
                (stream.ReadByte() << 16) |
                (stream.ReadByte() << 32);

            // read the rest of the message
            int readSize = await stream.ReadExactlyAsync(messageSize - headerSize, buffer);
            return readSize == messageSize - headerSize;
        }

    }
}
