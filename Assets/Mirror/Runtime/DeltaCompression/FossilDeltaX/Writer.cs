using System.Collections.Generic;

namespace FossilDeltaX
{
	public class Writer
	{
		List<byte> buffer;

		public Writer()
		{
			buffer = new List<byte>();
		}

		public void WriteByte(byte value)
		{
			buffer.Add(value);
		}

		public void WriteVarInt(ulong value)
		{
            if (value <= 240)
            {
                WriteByte((byte)value);
                return;
            }
            if (value <= 2287)
            {
                WriteByte((byte)(((value - 240) >> 8) + 241));
                WriteByte((byte)((value - 240) & 0xFF));
                return;
            }
            if (value <= 67823)
            {
                WriteByte((byte)249);
                WriteByte((byte)((value - 2288) >> 8));
                WriteByte((byte)((value - 2288) & 0xFF));
                return;
            }
            if (value <= 16777215)
            {
                WriteByte((byte)250);
                WriteByte((byte)(value & 0xFF));
                WriteByte((byte)((value >> 8) & 0xFF));
                WriteByte((byte)((value >> 16) & 0xFF));
                return;
            }
            if (value <= 4294967295)
            {
                WriteByte((byte)251);
                WriteByte((byte)(value & 0xFF));
                WriteByte((byte)((value >> 8) & 0xFF));
                WriteByte((byte)((value >> 16) & 0xFF));
                WriteByte((byte)((value >> 24) & 0xFF));
                return;
            }
            if (value <= 1099511627775)
            {
                WriteByte((byte)252);
                WriteByte((byte)(value & 0xFF));
                WriteByte((byte)((value >> 8) & 0xFF));
                WriteByte((byte)((value >> 16) & 0xFF));
                WriteByte((byte)((value >> 24) & 0xFF));
                WriteByte((byte)((value >> 32) & 0xFF));
                return;
            }
            if (value <= 281474976710655)
            {
                WriteByte((byte)253);
                WriteByte((byte)(value & 0xFF));
                WriteByte((byte)((value >> 8) & 0xFF));
                WriteByte((byte)((value >> 16) & 0xFF));
                WriteByte((byte)((value >> 24) & 0xFF));
                WriteByte((byte)((value >> 32) & 0xFF));
                WriteByte((byte)((value >> 40) & 0xFF));
                return;
            }
            if (value <= 72057594037927935)
            {
                WriteByte((byte)254);
                WriteByte((byte)(value & 0xFF));
                WriteByte((byte)((value >> 8) & 0xFF));
                WriteByte((byte)((value >> 16) & 0xFF));
                WriteByte((byte)((value >> 24) & 0xFF));
                WriteByte((byte)((value >> 32) & 0xFF));
                WriteByte((byte)((value >> 40) & 0xFF));
                WriteByte((byte)((value >> 48) & 0xFF));
                return;
            }

            // all others
            {
                WriteByte((byte)255);
                WriteByte((byte)(value & 0xFF));
                WriteByte((byte)((value >> 8) & 0xFF));
                WriteByte((byte)((value >> 16) & 0xFF));
                WriteByte((byte)((value >> 24) & 0xFF));
                WriteByte((byte)((value >> 32) & 0xFF));
                WriteByte((byte)((value >> 40) & 0xFF));
                WriteByte((byte)((value >> 48) & 0xFF));
                WriteByte((byte)((value >> 56) & 0xFF));
            }
		}

		public void WriteBytes(byte[] a, int offset, int count)
		{
			// TODO memcpy with an actual stream/buffer
			for (int i = 0; i < count; i++)
				buffer.Add(a[offset + i]);
		}

		public byte[] ToArray()
		{
			return buffer.ToArray();
		}
	}
}

