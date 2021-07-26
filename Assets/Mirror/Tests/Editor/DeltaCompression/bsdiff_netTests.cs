using System.IO;
using BsDiff;

namespace Mirror.Tests.DeltaCompression
{
    public class bsdiffnet_Tests : DeltaCompressionTests
    {
        public override void ComputeDelta(NetworkWriter from, NetworkWriter to, NetworkWriter result)
        {
            // TODO avoid .ToArray() copying for benchmark.
            byte[] fromArray = from.ToArray();
            byte[] toArray = to.ToArray();

            MemoryStream stream = new MemoryStream();
            BinaryPatchUtility.Create(fromArray, toArray, stream);

            byte[] delta = stream.ToArray();
            result.WriteBytes(delta, 0, delta.Length);
        }

        public override void ApplyPatch(NetworkWriter from, NetworkReader delta, NetworkWriter result)
        {
            // TODO avoid .ToArray() copying for benchmark.
            byte[] bytes = delta.ReadBytes(delta.Length);

            MemoryStream fromStream = new MemoryStream(from.ToArray());
            MemoryStream deltaStream = new MemoryStream(bytes);
            MemoryStream patchStream = new MemoryStream();
            BinaryPatchUtility.Apply(fromStream, () => deltaStream, patchStream);

            byte[] patched = patchStream.ToArray();
            result.WriteBytes(patched, 0, patched.Length);
        }
    }
}
