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

        public override void ApplyPatch(NetworkWriter from, NetworkWriter delta, NetworkWriter result)
        {
            // TODO avoid .ToArray() copying for benchmark.
            byte[] delta = Fossil.Delta.Apply(from.ToArray(), delta.ToArray());
            result.WriteBytes(delta, 0, delta.Length);
        }
    }
}
