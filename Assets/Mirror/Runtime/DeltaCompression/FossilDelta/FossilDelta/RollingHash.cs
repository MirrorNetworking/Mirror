using System;

namespace Fossil
{
	public class RollingHash
	{
		private UInt16 a;
		private UInt16 b;
		private UInt16 i;
		private byte[] z;
		static int ii = 0;

		public RollingHash ()
		{
			this.a = 0;
			this.b = 0;
			this.i = 0;
			this.z = new byte[Delta.NHASH];
		}

		/**
		 * Initialize the rolling hash using the first NHASH characters of z[]
		 */
		public void Init (byte[] z, int pos)
		{
			UInt16 a = 0, b = 0, i, x;
			for(i = 0; i < Delta.NHASH; i++){
				x = z[pos+i];
				a = (UInt16) ((a + x) & 0xffff);
				b = (UInt16) ((b + (Delta.NHASH-i)*x) & 0xffff);
				this.z[i] = (byte) x;
			}
			this.a = (UInt16) (a & 0xffff);
			this.b = (UInt16) (b & 0xffff);
			this.i = 0;
		}

		/**
		 * Advance the rolling hash by a single character "c"
		 */
		public void Next (byte c) {
			UInt16 old = this.z[this.i];
			this.z[this.i] = c;
			this.i = (UInt16) ((this.i+1)&(Delta.NHASH-1));
			this.a = (UInt16) (this.a - old + c);
			this.b = (UInt16) (this.b - Delta.NHASH*old + this.a);
		}


		/**
		 * Return a 32-bit hash value
		 */
		public UInt32 Value () {
			RollingHash.ii ++;
			return (UInt32) (((UInt32)(this.a & 0xffff)) | (((UInt32)(this.b & 0xffff)) << 16));
		}

		/*
		 * Compute a hash on NHASH bytes.
		 *
		 * This routine is intended to be equivalent to:
		 *    hash h;
		 *    hash_init(&h, zInput);
		 *    return hash_32bit(&h);
		 */
		public static UInt32 Once (byte[] z) {
			UInt16 a, b, i;
			a = b = z[0];
			for(i=1; i<Delta.NHASH; i++){
				a += z[i];
				b += a;
			}
			return a | (((UInt32)b)<<16);
		}
			
	}
}

