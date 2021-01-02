using Mirror;
using NUnit.Framework;
using UnityEngine;

namespace JamesFrowen.BitPacking.Tests
{
    public class BitWriterTest
    {
        const int max = (1 << 10) - 1;

        [Test]
        [Repeat(1000)]
        public void CanWrite32BitsRepeat()
        {
            uint inValue = (uint)Random.Range(0, int.MaxValue);

            NetworkWriter netWriter = new NetworkWriter();
            BitWriter writer = new BitWriter(netWriter);

            writer.Write(inValue, 32);

            writer.Flush();

            NetworkReader netReader = new NetworkReader(netWriter.ToArray());
            BitReader reader = new BitReader(netReader);

            uint outValue = reader.Read(32);

            Assert.That(outValue, Is.EqualTo(inValue));
        }

        [Test]
        [Repeat(1000)]
        public void CanWrite3MultipleValuesRepeat()
        {
            uint inValue1 = (uint)Random.Range(0, max);
            uint inValue2 = (uint)Random.Range(0, max);
            uint inValue3 = (uint)Random.Range(0, max);


            NetworkWriter netWriter = new NetworkWriter();
            BitWriter writer = new BitWriter(netWriter);

            writer.Write(inValue1, 10);
            writer.Write(inValue2, 10);
            writer.Write(inValue3, 10);

            writer.Flush();

            NetworkReader netReader = new NetworkReader(netWriter.ToArray());
            BitReader reader = new BitReader(netReader);

            uint outValue1 = reader.Read(10);
            uint outValue2 = reader.Read(10);
            uint outValue3 = reader.Read(10);

            Assert.That(outValue1, Is.EqualTo(inValue1), $"Failed [{inValue1},{inValue2},{inValue3}]");
            Assert.That(outValue2, Is.EqualTo(inValue2), $"Failed [{inValue1},{inValue2},{inValue3}]");
            Assert.That(outValue3, Is.EqualTo(inValue3), $"Failed [{inValue1},{inValue2},{inValue3}]");
        }

        [Test]
        [Repeat(1000)]
        public void CanWrite8MultipleValuesRepeat()
        {
            uint inValue1 = (uint)Random.Range(0, max);
            uint inValue2 = (uint)Random.Range(0, max);
            uint inValue3 = (uint)Random.Range(0, max);
            uint inValue4 = (uint)Random.Range(0, max);
            uint inValue5 = (uint)Random.Range(0, max);
            uint inValue6 = (uint)Random.Range(0, max);
            uint inValue7 = (uint)Random.Range(0, max);
            uint inValue8 = (uint)Random.Range(0, max);

            NetworkWriter netWriter = new NetworkWriter();
            BitWriter writer = new BitWriter(netWriter);

            writer.Write(inValue1, 10);
            writer.Write(inValue2, 10);
            writer.Write(inValue3, 10);
            writer.Write(inValue4, 10);
            writer.Write(inValue5, 10);
            writer.Write(inValue6, 10);
            writer.Write(inValue7, 10);
            writer.Write(inValue8, 10);

            writer.Flush();

            NetworkReader netReader = new NetworkReader(netWriter.ToArray());
            BitReader reader = new BitReader(netReader);

            uint outValue1 = reader.Read(10);
            uint outValue2 = reader.Read(10);
            uint outValue3 = reader.Read(10);
            uint outValue4 = reader.Read(10);
            uint outValue5 = reader.Read(10);
            uint outValue6 = reader.Read(10);
            uint outValue7 = reader.Read(10);
            uint outValue8 = reader.Read(10);

            Assert.That(outValue1, Is.EqualTo(inValue1), $"Failed [{inValue1},{inValue2},{inValue3},{inValue4},{inValue5},{inValue6},{inValue7},{inValue8}]");
            Assert.That(outValue2, Is.EqualTo(inValue2), $"Failed [{inValue1},{inValue2},{inValue3},{inValue4},{inValue5},{inValue6},{inValue7},{inValue8}]");
            Assert.That(outValue3, Is.EqualTo(inValue3), $"Failed [{inValue1},{inValue2},{inValue3},{inValue4},{inValue5},{inValue6},{inValue7},{inValue8}]");
            Assert.That(outValue4, Is.EqualTo(inValue4), $"Failed [{inValue1},{inValue2},{inValue3},{inValue4},{inValue5},{inValue6},{inValue7},{inValue8}]");
            Assert.That(outValue5, Is.EqualTo(inValue5), $"Failed [{inValue1},{inValue2},{inValue3},{inValue4},{inValue5},{inValue6},{inValue7},{inValue8}]");
            Assert.That(outValue6, Is.EqualTo(inValue6), $"Failed [{inValue1},{inValue2},{inValue3},{inValue4},{inValue5},{inValue6},{inValue7},{inValue8}]");
            Assert.That(outValue7, Is.EqualTo(inValue7), $"Failed [{inValue1},{inValue2},{inValue3},{inValue4},{inValue5},{inValue6},{inValue7},{inValue8}]");
            Assert.That(outValue8, Is.EqualTo(inValue8), $"Failed [{inValue1},{inValue2},{inValue3},{inValue4},{inValue5},{inValue6},{inValue7},{inValue8}]");
        }

        [Test]
        [Description("these are failed random cases")]
        [TestCase(859u, 490u, 45u, 583u, 153u, 321u, 147u, 305u)]
        [TestCase(360u, 454u, 105u, 949u, 194u, 312u, 272u, 350u)]
        public void CanWrite8MultipleValues(uint inValue1, uint inValue2, uint inValue3, uint inValue4, uint inValue5, uint inValue6, uint inValue7, uint inValue8)
        {
            NetworkWriter netWriter = new NetworkWriter();
            BitWriter writer = new BitWriter(netWriter);

            writer.Write(inValue1, 10);
            writer.Write(inValue2, 10);
            writer.Write(inValue3, 10);
            writer.Write(inValue4, 10);
            writer.Write(inValue5, 10);
            writer.Write(inValue6, 10);
            writer.Write(inValue7, 10);
            writer.Write(inValue8, 10);

            writer.Flush();

            NetworkReader netReader = new NetworkReader(netWriter.ToArray());
            BitReader reader = new BitReader(netReader);

            uint outValue1 = reader.Read(10);
            Assert.That(outValue1, Is.EqualTo(inValue1), $"Failed [{inValue1},{inValue2},{inValue3},{inValue4},{inValue5},{inValue6},{inValue7},{inValue8}]");

            uint outValue2 = reader.Read(10);
            Assert.That(outValue2, Is.EqualTo(inValue2), $"Failed [{inValue1},{inValue2},{inValue3},{inValue4},{inValue5},{inValue6},{inValue7},{inValue8}]");

            uint outValue3 = reader.Read(10);
            Assert.That(outValue3, Is.EqualTo(inValue3), $"Failed [{inValue1},{inValue2},{inValue3},{inValue4},{inValue5},{inValue6},{inValue7},{inValue8}]");

            uint outValue4 = reader.Read(10);
            Assert.That(outValue4, Is.EqualTo(inValue4), $"Failed [{inValue1},{inValue2},{inValue3},{inValue4},{inValue5},{inValue6},{inValue7},{inValue8}]");

            uint outValue5 = reader.Read(10);
            Assert.That(outValue5, Is.EqualTo(inValue5), $"Failed [{inValue1},{inValue2},{inValue3},{inValue4},{inValue5},{inValue6},{inValue7},{inValue8}]");

            uint outValue6 = reader.Read(10);
            Assert.That(outValue6, Is.EqualTo(inValue6), $"Failed [{inValue1},{inValue2},{inValue3},{inValue4},{inValue5},{inValue6},{inValue7},{inValue8}]");

            uint outValue7 = reader.Read(10);
            Assert.That(outValue7, Is.EqualTo(inValue7), $"Failed [{inValue1},{inValue2},{inValue3},{inValue4},{inValue5},{inValue6},{inValue7},{inValue8}]");

            uint outValue8 = reader.Read(10);
            Assert.That(outValue8, Is.EqualTo(inValue8), $"Failed [{inValue1},{inValue2},{inValue3},{inValue4},{inValue5},{inValue6},{inValue7},{inValue8}]");
        }
    }
}
