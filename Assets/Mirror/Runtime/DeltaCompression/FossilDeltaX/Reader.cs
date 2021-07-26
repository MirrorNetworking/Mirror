using System;

namespace FossilDeltaX
{
	public class Reader
	{
		public byte[] a;
		public uint pos;

		public Reader(byte[] array)
		{
			a = array;
			pos = 0;
		}

		public bool HaveBytes()
		{
			return pos < a.Length;
		}

		public byte GetByte()
		{
			byte b = a[pos];
			pos++;
			if (pos > a.Length)
				throw new IndexOutOfRangeException("out of bounds");
			return b;
		}

		// Read bytes from *pz and convert them into a positive integer.  When
		// finished, leave *pz pointing to the first character past the end of
		// the integer.  The *pLen parameter holds the length of the string
		// in *pz and is decremented once for each character in the integer.
		public uint GetInt()
		{
			// original code checked HaveBytes before every byte.
			// and would return a partial int if only 3 havebytes.
			// let's do that too for now.
			uint value = 0;
			if (HaveBytes()) value |= GetByte();
			if (HaveBytes()) value |= (uint)(GetByte() << 8);
			if (HaveBytes()) value |= (uint)(GetByte() << 16);
			if (HaveBytes()) value |= (uint)(GetByte() << 24);
			return value;
		}
	}
}

