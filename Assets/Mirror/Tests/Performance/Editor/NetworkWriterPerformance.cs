#if !UNITY_2019_2_OR_NEWER || UNITY_PERFORMANCE_TESTS_1_OR_OLDER
using NUnit.Framework;
using UnityEngine;
using Unity.PerformanceTesting;

namespace Mirror.Tests.Performance
{
    [Category("Performance")]
    public class NetworkWriterPerformance
    {
        // A Test behaves as an ordinary method
        [Test]
#if UNITY_2019_2_OR_NEWER
        [Performance]
#else
        [PerformanceTest]
#endif
        public void RunWriteInt32()
        {
            Measure.Method(WriteInt32)
                .WarmupCount(10)
                .MeasurementCount(100)
                .Run();
        }

        static void WriteInt32()
        {
            NetworkWriter writer = new NetworkWriter();
            for (int i = 0; i < 100000; i++)
            {
                writer.WriteInt32(i);
            }
        }
        // A Test behaves as an ordinary method
        [Test]
#if UNITY_2019_2_OR_NEWER
        [Performance]
#else
        [PerformanceTest]
#endif
        public void RunWriteQuaternion()
        {
            Measure.Method(WriteQuaternion)
                .WarmupCount(10)
                .MeasurementCount(100)
                .Run();
        }

        static void WriteQuaternion()
        {
            NetworkWriter writer = new NetworkWriter();
            for (int i = 0; i < 100000; i++)
            {
                writer.WriteQuaternion(Quaternion.identity);
            }
        }
    }
}
#endif
