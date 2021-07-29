using System.IO;
using FossilDeltaX;

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

        // overwrite Delta benchmark for nonalloc version
        public override void ComputeDeltaBenchmark(NetworkWriter writerA, NetworkWriter writerB, int amount, NetworkWriter result)
        {
            ArraySegmentX<byte> ASegment = writerA.ToArraySegment();
            ArraySegmentX<byte> BSegment = writerB.ToArraySegment();
            int[] collide = new int[0];
            int[] landmark = new int[0];
            MemoryStream stream = new MemoryStream();

            for (int i = 0; i < amount; ++i)
            {
                // reset write each time. don't want to measure resizing.
                result.Position = 0;
                stream.Position = 0;

                // nonalloc delta
                FossilDeltaX.Delta.Create(ASegment, BSegment, ref collide, ref landmark, stream);
                // copy to result for fairness. need to do in Mirror later too.
                ArraySegmentX<byte> streamSegment = new ArraySegmentX<byte>(stream.GetBuffer(), 0, (int)stream.Position);
                result.WriteBytes(streamSegment.Array, streamSegment.Offset, streamSegment.Count);
            }
        }
    }
}
