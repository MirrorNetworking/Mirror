namespace Mirror.Tests.DeltaCompression
{
    public class FossilDeltaCompressionTests : DeltaCompressionTests
    {
        public override void ComputeDelta(NetworkWriter from, NetworkWriter to, NetworkWriter result)
        {
            // TODO avoid .ToArray() copying for benchmark.
            byte[] delta = Fossil.Delta.Create(from.ToArray(), to.ToArray());
            result.WriteBytes(delta, 0, delta.Length);
        }

        public override void ApplyPatch(NetworkWriter from, NetworkReader delta, NetworkWriter result)
        {
            // TODO avoid .ToArray() copying for benchmark.
            byte[] bytes = delta.ReadBytes(delta.Length);
            byte[] patched = Fossil.Delta.Apply(from.ToArray(), bytes);
            result.WriteBytes(patched, 0, patched.Length);
        }
    }
}
