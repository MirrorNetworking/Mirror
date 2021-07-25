using System.Collections.Generic;
using Octodiff.Core;

namespace Mirror.Tests.DeltaCompression
{
    public class rdiffXTests : DeltaCompressionTests
    {
        const int ChunkSize = 96;

        public override void ComputeDelta(NetworkWriter from, NetworkWriter to, NetworkWriter result)
        {
            // TODO avoid .ToArray() copying for benchmark.
            byte[] fromArray = from.ToArray();
            byte[] toArray = to.ToArray();

            List<ChunkSignature> signature = new List<ChunkSignature>();
            Operations.CreateSignature(fromArray, ChunkSize, signature);
            byte[] delta = Operations.CreateDelta(fromArray, signature, toArray);
            result.WriteBytes(delta, 0, delta.Length);
        }

        public override void ApplyPatch(NetworkWriter from, NetworkReader delta, NetworkWriter result)
        {
            // TODO avoid .ToArray() copying for benchmark.
            byte[] fromArray = from.ToArray();
            byte[] bytes = delta.ReadBytes(delta.Length);

            byte[] patched = Operations.Patch(fromArray, bytes);
            result.WriteBytes(patched, 0, patched.Length);
        }
    }
}
