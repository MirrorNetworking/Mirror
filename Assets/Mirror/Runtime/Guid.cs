using System;
namespace Mirror
{
	[System.Serializable]
	public struct Guid : IEquatable<Guid>
	{
		public ulong hash1, hash2;
		public static Guid Empty = default;
		public Guid(ulong h1, ulong h2)
		{
			hash1 = h1;
			hash2 = h2;
		}

		public bool Equals(Guid o) => hash1 == o.hash1 && hash2 == o.hash2;
		public override bool Equals(object o) => o is Guid g && Equals(g);
		public static bool operator ==(Guid a, Guid b) => a.Equals(b);
		public static bool operator !=(Guid a, Guid b) => !a.Equals(b);
		public override string ToString() => string.Format("{0:X}-{1:X}", hash1, hash2);
		public override int GetHashCode() => (int) hash1;
	}
}