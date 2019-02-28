using System;
namespace Mirror
{
	[System.Serializable]
	public struct NetworkGuid : IEquatable<NetworkGuid>
	{
		public ulong hash1, hash2;
		public static NetworkGuid Empty = default;
		public static NetworkGuid NewGuid() => FromGuid(System.Guid.NewGuid());
		public NetworkGuid(ulong h1, ulong h2)
		{
			hash1 = h1;
			hash2 = h2;
		}
		public static NetworkGuid FromReader(NetworkReader nr) => new NetworkGuid(nr.ReadUInt64(), nr.ReadUInt64());
		public static NetworkGuid FromGuid(Guid guid) => FromReader(new NetworkReader(guid.ToByteArray()));

		public bool Equals(NetworkGuid o) => hash1 == o.hash1 && hash2 == o.hash2;
		public override bool Equals(object o) => o is NetworkGuid g && Equals(g);
		public static bool operator ==(NetworkGuid a, NetworkGuid b) => a.Equals(b);
		public static bool operator !=(NetworkGuid a, NetworkGuid b) => !a.Equals(b);
		public override string ToString() => string.Format("{0:X}-{1:X}", hash1, hash2);
		public override int GetHashCode() => (int) hash1;
	}
}