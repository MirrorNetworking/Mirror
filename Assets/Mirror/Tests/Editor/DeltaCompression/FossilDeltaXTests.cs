namespace Mirror.Tests.DeltaCompression
{
    public class FossilDeltaXTests : DeltaCompressionTests
    {
        public override void ComputeDelta(NetworkWriter from, NetworkWriter to, NetworkWriter result)
        {
            // TODO avoid .ToArray() copying for benchmark.
            byte[] delta = FossilDeltaX.Delta.Create(from.ToArray(), to.ToArray());
            result.WriteBytes(delta, 0, delta.Length);
        }

        public override void ApplyPatch(NetworkWriter from, NetworkReader delta, NetworkWriter result)
        {
            // TODO avoid .ToArray() copying for benchmark.
            byte[] bytes = delta.ReadBytes(delta.Length);
            byte[] patched = FossilDeltaX.Delta.Apply(from.ToArray(), bytes);
            result.WriteBytes(patched, 0, patched.Length);
        }
    }
}
