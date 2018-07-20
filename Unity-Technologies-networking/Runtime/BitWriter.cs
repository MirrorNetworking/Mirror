#if ENABLE_UNET
using System.IO;

namespace UnityEngine.Networking
{
    // writes a single bit to the stream. Consecutive bits are packed together
    // in the same byte to reduce message size. Especially useful for bools which
    // don't need to use an entire byte for each.
    public class BitWriter : BinaryWriter
    {
        private byte currentByte;
        private byte currentBitIndex = 0;

        public BitWriter(Stream s) : base(s) { }

        public override void Flush()
        {
            FlushBuffer();
            base.Flush();
        }

        public override void Write(byte[] buffer, int index, int count)
        {
            FlushBuffer();
            base.Write(buffer, index, count);
        }

        public override void Write(byte value)
        {
            FlushBuffer();
            base.Write(value);
        }

        public override void Write(bool value)
        {
            byte bit = value ? (byte)1 : (byte)0;
            currentByte |= (byte)(bit << currentBitIndex);
            currentBitIndex++;

            // if actual byte is full, we start a new one
            if (currentBitIndex == 8)
            {
                FlushBuffer();
            }
        }

        public override void Write(string value)
        {
            FlushBuffer();
            base.Write(value);
        }

        public override void Write(byte[] value)
        {
            FlushBuffer();
            base.Write(value);
        }

        public override void Write(sbyte value)
        {
            FlushBuffer();
            base.Write(value);
        }

        public override void Write(short value)
        {
            FlushBuffer();
            base.Write(value);
        }

        public override void Write(ushort value)
        {
            FlushBuffer();
            base.Write(value);
        }

        public override void Write(int value)
        {
            FlushBuffer();
            base.Write(value);
        }

        public override void Write(uint value)
        {
            FlushBuffer();
            base.Write(value);
        }

        public override void Write(long value)
        {
            FlushBuffer();
            base.Write(value);
        }

        public override void Write(ulong value)
        {
            FlushBuffer();
            base.Write(value);
        }

        public override void Write(float value)
        {
            FlushBuffer();
            base.Write(value);
        }

        public override void Write(decimal value)
        {
            FlushBuffer();
            base.Write(value);
        }

        public override void Write(double value)
        {
            FlushBuffer();
            base.Write(value);
        }

        public override void Write(char value)
        {
            FlushBuffer();
            base.Write(value);
        }

        public override void Write(char[] chars)
        {
            FlushBuffer();
            base.Write(chars);
        }

        public override void Write(char[] buffer, int index, int count)
        {
            FlushBuffer();
            base.Write(buffer, index, count);
        }

        void FlushBuffer()
        {
            if (currentBitIndex == 0) // nothing to flush
                return;

            base.Write(currentByte);
            currentByte = 0;
            currentBitIndex = 0;
        }
    }
}
#endif //ENABLE_UNET