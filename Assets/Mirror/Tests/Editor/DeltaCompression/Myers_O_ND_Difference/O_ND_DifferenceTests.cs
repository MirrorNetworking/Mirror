using O_ND_Difference;

namespace Mirror.Tests.DeltaCompression.Myers_O_ND_Difference
{
    public class O_ND_DifferenceTests : DeltaCompressionTests
    {
        public override void ComputeDelta(NetworkWriter from, NetworkWriter to, NetworkWriter result)
        {
            // algorithm needs int[] for now
            byte[] fromBytes = from.ToArray();
            int[] fromInts = new int[fromBytes.Length];
            for (int i = 0; i < fromBytes.Length; ++i)
                fromInts[i] = fromBytes[i];

            byte[] toBytes = to.ToArray();
            int[] toInts = new int[toBytes.Length];
            for (int i = 0; i < toBytes.Length; ++i)
                toInts[i] = toBytes[i];

            Diff.Item[] items = Diff.DiffInt(fromInts, toInts);
        }

        public override void ApplyPatch(NetworkWriter from, NetworkWriter patch, NetworkWriter result)
        {
            throw new System.NotImplementedException();
        }
    }
}
