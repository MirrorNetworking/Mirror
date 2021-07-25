using System;
using System.Collections;

namespace Fossil
{
	public class Reader
	{
		static readonly int[] zValue = {
			-1, -1, -1, -1, -1, -1, -1, -1,   -1, -1, -1, -1, -1, -1, -1, -1,
			-1, -1, -1, -1, -1, -1, -1, -1,   -1, -1, -1, -1, -1, -1, -1, -1,
			-1, -1, -1, -1, -1, -1, -1, -1,   -1, -1, -1, -1, -1, -1, -1, -1,
			0,  1,  2,  3,  4,  5,  6,  7,    8,  9, -1, -1, -1, -1, -1, -1,
			-1, 10, 11, 12, 13, 14, 15, 16,   17, 18, 19, 20, 21, 22, 23, 24,
			25, 26, 27, 28, 29, 30, 31, 32,   33, 34, 35, -1, -1, -1, -1, 36,
			-1, 37, 38, 39, 40, 41, 42, 43,   44, 45, 46, 47, 48, 49, 50, 51,
			52, 53, 54, 55, 56, 57, 58, 59,   60, 61, 62, -1, -1, -1, 63, -1
		};
			
		public byte[] a;
		public uint pos;

		public Reader (byte[] array)
		{
			this.a = array;
			this.pos = 0;
		}

		public bool HaveBytes () 
		{
			return this.pos < this.a.Length;
		}

		public byte GetByte () 
		{
			byte b = this.a[this.pos];
			this.pos++;
			if (this.pos > this.a.Length) 
				throw new IndexOutOfRangeException("out of bounds");
			return b;
		}

		public char GetChar() 
		{
			//  return String.fromCharCode(this.getByte());
			return (char) this.GetByte();
		}

		/**
		 * Read bytes from *pz and convert them into a positive integer.  When
		 * finished, leave *pz pointing to the first character past the end of
		 * the integer.  The *pLen parameter holds the length of the string
		 * in *pz and is decremented once for each character in the integer.
		 */
		public uint GetInt ()
		{
			uint v = 0;
			int c;
			while(this.HaveBytes() && (c = zValue[0x7f & this.GetByte()]) >= 0) {
				v = (uint) ((((Int32) v) << 6) + c);
			}
			this.pos--;
			return v;
		}
	}
}

