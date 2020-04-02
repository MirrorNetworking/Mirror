#if UNITY_2019_2_OR_NEWER
using Mirror;
using NUnit.Framework;
using Unity.PerformanceTesting;

namespace Tests
{
    public class NetworkWriterPerformance
    {
        // A Test behaves as an ordinary method
        [Test]
        [Performance]
        public void WritePackedInt32()
        {
            Measure.Method(WriteInt32)
                .WarmupCount(10)
                .MeasurementCount(100)
                .IterationsPerMeasurement(10000)
                .GC()
                .Run();
        }

        void WriteInt32()
        {
            using (PooledNetworkWriter writer = NetworkWriterPool.GetWriter())
            {
                for (int i = 0; i < 10; i++)
                {
                    writer.WritePackedInt32(i * 1000);
                }
            }
        }
    }
}
#endif
