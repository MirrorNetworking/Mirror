using System;

namespace FossilDeltaX
{
	// struct to avoid runtime heap allocations
	// -> pass it as 'ref'!
	public struct Reader
	{
		public ArraySegmentX<byte> buffer;
		public uint Position;

		public Reader(ArraySegmentX<byte> segment)
		{
			buffer = segment;
			Position = 0;
		}

		public bool HaveBytes() => Position < buffer.Count;

		public byte ReadByte()
		{
			byte b = buffer.Array[buffer.Offset + Position];
			Position++;
			if (Position > buffer.Count)
				throw new IndexOutOfRangeException("out of bounds");
			return b;
		}
	}
}

