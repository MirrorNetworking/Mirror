// https://fossil-scm.org/home/doc/trunk/www/delta_encoder_algorithm.wiki
// rolling hash is useful to hash through sliding windows.
namespace FossilDeltaX
{
	public unsafe struct RollingHash
	{
		ushort a;
		ushort b;
		ushort i;

		// fixed buffer of HASH_SIZE to avoid heap allocations
		fixed byte z[Delta.HASH_SIZE];

		// Initialize the rolling hash using the first NHASH characters of z[]
		public void Init(ArraySegmentX<byte> z, int pos)
		{
			ushort a = 0, b = 0, i, x;
			for(i = 0; i < Delta.HASH_SIZE; i++)
			{
				x = z.Array[z.Offset + pos + i];
				a = (ushort) ((a + x) & 0xffff);
				b = (ushort) ((b + (Delta.HASH_SIZE-i)*x) & 0xffff);
				this.z[i] = (byte) x;
			}
			this.a = (ushort)(a & 0xffff);
			this.b = (ushort)(b & 0xffff);
			this.i = 0;
		}

		// Advance the rolling hash by a single character "c"
		public void Next(byte c)
		{
			ushort old = z[i];
			z[i] = c;
			i = (ushort)((i+1) & (Delta.HASH_SIZE-1));
			a = (ushort)(a - old + c);
			b = (ushort)(b - Delta.HASH_SIZE*old + a);
		}

		// Return a 32-bit hash value
		public uint Value()
		{
			return ((uint)(a & 0xffff)) | (((uint)(b & 0xffff)) << 16);
		}
	}
}

