using NUnit.Framework;
using Unity.PerformanceTesting;
using Mirror.KCP;

namespace Mirror.Tests.Performance
{
    public class KcpEditorPerformance
    {
        [Test]
        [Performance]
        public void WriteBytes()
        {
            Measure.Method(WBytes)
                .WarmupCount(10)
                .MeasurementCount(1000)
                .Run();
        }

        static void WBytes()
        {
            var buffer = new ByteBuffer(1024);
            byte[] bytes = {0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF};

            for (int i = 0; i < 10000; i++)
            {
                buffer.WriteBytes(bytes, 3, 3);
            }
        }
    }
}
