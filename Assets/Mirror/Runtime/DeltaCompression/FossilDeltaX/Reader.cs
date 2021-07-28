using System;

namespace FossilDeltaX
{
	public class Reader
	{
		public ArraySegmentX<byte> buffer;
		public uint pos;

		public Reader(ArraySegmentX<byte> array)
		{
			buffer = array;
			pos = 0;
		}

		public bool HaveBytes() => pos < buffer.Count;

		public byte ReadByte()
		{
			byte b = buffer.Array[buffer.Offset + pos];
			pos++;
			if (pos > buffer.Count)
				throw new IndexOutOfRangeException("out of bounds");
			return b;
		}
	}
}

