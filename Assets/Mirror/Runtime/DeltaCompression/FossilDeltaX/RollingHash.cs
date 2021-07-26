namespace FossilDeltaX
{
	public class RollingHash
	{
		ushort a;
		ushort b;
		ushort i;
		byte[] z;

		public RollingHash()
		{
			a = 0;
			b = 0;
			i = 0;
			z = new byte[Delta.HASHSIZE];
		}

		// Initialize the rolling hash using the first NHASH characters of z[]
		public void Init(byte[] z, int pos)
		{
			ushort a = 0, b = 0, i, x;
			for(i = 0; i < Delta.HASHSIZE; i++)
			{
				x = z[pos + i];
				a = (ushort) ((a + x) & 0xffff);
				b = (ushort) ((b + (Delta.HASHSIZE-i)*x) & 0xffff);
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
			i = (ushort) ((i+1) & (Delta.HASHSIZE-1));
			a = (ushort) (a - old + c);
			b = (ushort) (b - Delta.HASHSIZE*old + a);
		}

		// Return a 32-bit hash value
		public uint Value()
		{
			return ((uint)(a & 0xffff)) | (((uint)(b & 0xffff)) << 16);
		}
	}
}

