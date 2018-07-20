#if ENABLE_UNET
using System.IO;

namespace UnityEngine.Networking
{
    // reads packed bits in the same byte
    public class BitReader : BinaryReader
    {
        private byte currentByte;
        private byte currentBitIndex = 8;

        public BitReader(Stream s) : base(s) { }

        public override byte ReadByte()
        {
            currentBitIndex = 8; // to be sure to read a whole new byte
            return base.ReadByte();
        }

        public override byte[] ReadBytes(int count)
        {
            currentBitIndex = 8; // to be sure to read a whole new byte
            return base.ReadBytes(count);
        }

        public override sbyte ReadSByte()
        {
            currentBitIndex = 8; // to be sure to read a whole new byte
            return base.ReadSByte();
        }

        public override int Read(byte[] buffer, int index, int count)
        {
            currentBitIndex = 8; // to be sure to read a whole new byte
            return base.Read(buffer, index, count);
        }

        public override bool ReadBoolean()
        {
            if (currentBitIndex == 8)
            {
                // when actual byte is full, we load a new one 
                currentByte = ReadByte();
                currentBitIndex = 0;
            }

            bool b = (currentByte & (1 << currentBitIndex)) != 0;
            currentBitIndex++;
            return b;
        }

        public override string ReadString()
        {
            currentBitIndex = 8; // to be sure to read a whole new byte
            return base.ReadString();
        }

        public override short ReadInt16()
        {
            currentBitIndex = 8; // to be sure to read a whole new byte
            return base.ReadInt16();
        }

        public override ushort ReadUInt16()
        {
            currentBitIndex = 8; // to be sure to read a whole new byte
            return base.ReadUInt16();
        }

        public override int ReadInt32()
        {
            currentBitIndex = 8; // to be sure to read a whole new byte
            return base.ReadInt32();
        }

        public override uint ReadUInt32()
        {
            currentBitIndex = 8; // to be sure to read a whole new byte
            return base.ReadUInt32();
        }

        public override long ReadInt64()
        {
            currentBitIndex = 8; // to be sure to read a whole new byte
            return base.ReadInt64();
        }

        public override ulong ReadUInt64()
        {
            currentBitIndex = 8; // to be sure to read a whole new byte
            return base.ReadUInt64();
        }

        public override float ReadSingle()
        {
            currentBitIndex = 8; // to be sure to read a whole new byte
            return base.ReadSingle();
        }

        public override decimal ReadDecimal()
        {
            currentBitIndex = 8; // to be sure to read a whole new byte
            return base.ReadDecimal();
        }

        public override double ReadDouble()
        {
            currentBitIndex = 8; // to be sure to read a whole new byte
            return base.ReadDouble();
        }

        public override char ReadChar()
        {
            currentBitIndex = 8; // to be sure to read a whole new byte
            return base.ReadChar(); 
        }

        public override char[] ReadChars(int count)
        {
            currentBitIndex = 8; // to be sure to read a whole new byte
            return base.ReadChars(count);
        }

        public override int Read(char[] buffer, int index, int count)
        {
            currentBitIndex = 8; // to be sure to read a whole new byte
            return base.Read(buffer, index, count);
        }
    }
}
#endif //ENABLE_UNET