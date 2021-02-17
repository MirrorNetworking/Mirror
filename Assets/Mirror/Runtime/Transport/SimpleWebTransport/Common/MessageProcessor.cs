using System.IO;
using System.Runtime.CompilerServices;

namespace Mirror.SimpleWeb
{
    public static class MessageProcessor
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static byte FirstLengthByte(byte[] buffer) => (byte)(buffer[1] & 0b0111_1111);

        public static bool NeedToReadShortLength(byte[] buffer)
        {
            byte lenByte = FirstLengthByte(buffer);

            return lenByte >= Constants.UshortPayloadLength;
        }

        public static int GetOpcode(byte[] buffer)
        {
            return buffer[0] & 0b0000_1111;
        }

        public static int GetPayloadLength(byte[] buffer)
        {
            byte lenByte = FirstLengthByte(buffer);
            return GetMessageLength(buffer, 0, lenByte);
        }

        public static void ValidateHeader(byte[] buffer, int maxLength, bool expectMask)
        {
            bool finished = (buffer[0] & 0b1000_0000) != 0; // has full message been sent
            bool hasMask = (buffer[1] & 0b1000_0000) != 0; // true from clients, false from server, "All messages from the client to the server have this bit set"

            int opcode = buffer[0] & 0b0000_1111; // expecting 1 - text message
            byte lenByte = FirstLengthByte(buffer);

            ThrowIfNotFinished(finished);
            ThrowIfMaskNotExpected(hasMask, expectMask);
            ThrowIfBadOpCode(opcode);

            int msglen = GetMessageLength(buffer, 0, lenByte);

            ThrowIfLengthZero(msglen);
            ThrowIfMsgLengthTooLong(msglen, maxLength);
        }

        public static void ToggleMask(byte[] src, int sourceOffset, int messageLength, byte[] maskBuffer, int maskOffset)
        {
            ToggleMask(src, sourceOffset, src, sourceOffset, messageLength, maskBuffer, maskOffset);
        }

        public static void ToggleMask(byte[] src, int sourceOffset, ArrayBuffer dst, int messageLength, byte[] maskBuffer, int maskOffset)
        {
            ToggleMask(src, sourceOffset, dst.array, 0, messageLength, maskBuffer, maskOffset);
            dst.count = messageLength;
        }

        public static void ToggleMask(byte[] src, int srcOffset, byte[] dst, int dstOffset, int messageLength, byte[] maskBuffer, int maskOffset)
        {
            for (int i = 0; i < messageLength; i++)
            {
                byte maskByte = maskBuffer[maskOffset + i % Constants.MaskSize];
                dst[dstOffset + i] = (byte)(src[srcOffset + i] ^ maskByte);
            }
        }

        /// <exception cref="InvalidDataException"></exception>
        static int GetMessageLength(byte[] buffer, int offset, byte lenByte)
        {
            if (lenByte == Constants.UshortPayloadLength)
            {
                // header is 4 bytes long
                ushort value = 0;
                value |= (ushort)(buffer[offset + 2] << 8);
                value |= buffer[offset + 3];

                return value;
            }
            else if (lenByte == Constants.UlongPayloadLength)
            {
                throw new InvalidDataException("Max length is longer than allowed in a single message");
            }
            else // is less than 126
            {
                // header is 2 bytes long
                return lenByte;
            }
        }

        /// <exception cref="InvalidDataException"></exception>
        static void ThrowIfNotFinished(bool finished)
        {
            if (!finished)
            {
                throw new InvalidDataException("Full message should have been sent, if the full message wasn't sent it wasn't sent from this trasnport");
            }
        }

        /// <exception cref="InvalidDataException"></exception>
        static void ThrowIfMaskNotExpected(bool hasMask, bool expectMask)
        {
            if (hasMask != expectMask)
            {
                throw new InvalidDataException($"Message expected mask to be {expectMask} but was {hasMask}");
            }
        }

        /// <exception cref="InvalidDataException"></exception>
        static void ThrowIfBadOpCode(int opcode)
        {
            // 2 = binary
            // 8 = close
            if (opcode != 2 && opcode != 8)
            {
                throw new InvalidDataException("Expected opcode to be binary or close");
            }
        }

        /// <exception cref="InvalidDataException"></exception>
        static void ThrowIfLengthZero(int msglen)
        {
            if (msglen == 0)
            {
                throw new InvalidDataException("Message length was zero");
            }
        }

        /// <summary>
        /// need to check this so that data from previous buffer isn't used
        /// </summary>
        /// <exception cref="InvalidDataException"></exception>
        static void ThrowIfMsgLengthTooLong(int msglen, int maxLength)
        {
            if (msglen > maxLength)
            {
                throw new InvalidDataException("Message length is greater than max length");
            }
        }
    }
}
