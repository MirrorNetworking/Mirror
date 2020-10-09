using System.IO;

namespace Mirror.SimpleWeb
{
    public static class MessageProcessor
    {
        public struct Result
        {
            public int opcode;
            public bool hasMask;
            /// <summary>
            /// offset after length in header (2 or 4 based on length + segment offset)
            /// </summary>
            public int offset;
            public int msgLength;

            /// <summary>
            /// how much more data there is to read
            /// </summary>
            /// <remarks>
            /// 4 bytes have already been read for header.
            /// header could be 2 or 4 bytes long so add (offset - 4) at account for part of mask/msg already being read
            /// </remarks>
            public int readLength => (offset - Constants.HeaderSize) + (hasMask ? Constants.MaskSize : 0) + msgLength;

            /// <summary>
            /// when message starts
            /// </summary>
            public int maskOffset => hasMask ? offset : throw new InvalidDataException("this header has no mask");

            /// <summary>
            /// when message starts
            /// </summary>
            public int msgOffset => hasMask ? offset + Constants.MaskSize : offset;
        }

        public static byte GetBytePayloadLength(byte[] buffer)
        {
            byte lenByte = (byte)(buffer[1] & 0b0111_1111); // first length byte

            return lenByte;
        }

        /// <exception cref="InvalidDataException"></exception>
        public static Result ProcessHeader(byte[] buffer, int maxLength, bool expectMask)
        {
            bool finished = (buffer[0] & 0b1000_0000) != 0; // has full message been sent
            bool hasMask = (buffer[1] & 0b1000_0000) != 0; // must be true, "All messages from the client to the server have this bit set"

            int opcode = buffer[0] & 0b0000_1111; // expecting 1 - text message
            byte lenByte = (byte)(buffer[1] & 0b0111_1111); // first length byte

            ThrowIfNotFinished(finished);
            ThrowIfMaskNotExpected(hasMask, expectMask);
            ThrowIfBadOpCode(opcode);

            // offset is 2 or 4
            (int msglen, int offsetAfterLength) = GetMessageLength(buffer, 0, lenByte);

            ThrowIfLengthZero(msglen);
            ThrowIfMsgLengthTooLong(msglen, maxLength);

            return new Result
            {
                opcode = opcode,
                offset = offsetAfterLength,
                hasMask = hasMask,
                msgLength = msglen,
            };
        }

        public static void ToggleMask(byte[] messageBuffer, int messageOffset, int messageLength, byte[] maskBuffer, int maskOffset)
        {
            for (int i = 0; i < messageLength; i++)
            {
                byte maskByte = maskBuffer[maskOffset + i % Constants.MaskSize];
                messageBuffer[messageOffset + i] = (byte)(messageBuffer[messageOffset + i] ^ maskByte);
            }
        }

        /// <exception cref="InvalidDataException"></exception>
        static (int length, int offsetAfterLength) GetMessageLength(byte[] buffer, int offset, byte lenByte)
        {
            if (lenByte == Constants.UshortPayloadLength)
            {
                // header is 4 bytes long
                ushort value = 0;
                value |= (ushort)(buffer[offset + 2] << 8);
                value |= buffer[offset + 3];

                return (value, offset + 4);
            }
            else if (lenByte == Constants.UlongPayloadLength)
            {
                throw new InvalidDataException("Max length is longer than allowed in a single message");
            }
            else // is less than 126
            {
                // header is 2 bytes long
                return (lenByte, offset + 2);
            }
        }

        /// <exception cref="InvalidDataException"></exception>
        static void ThrowIfNotFinished(bool finished)
        {
            if (!finished)
            {
                // TODO check if we need to deal with this
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
        /// need to check this so that data from previous buffer isnt used
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
