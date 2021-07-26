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

		public void WriteInt(uint value)
		{
			WriteByte((byte)value);
			WriteByte((byte)(value >> 8));
			WriteByte((byte)(value >> 16));
			WriteByte((byte)(value >> 24));
		}

		public void WriteBytes(byte[] a, int offset, int count)
		{
			// TODO memcpy with an actual stream/buffer
			for (int i = 0; i < count; i++)
				this.buffer.Add(a[offset + i]);
		}

		public byte[] ToArray()
		{
			return buffer.ToArray();
		}
	}
}

