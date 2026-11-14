using NUnit.Framework;

namespace Mirror.Tests.Tools
{
    public class DeltaCompressionTests
    {
        [Test]
        public void Compress_Long_Unchanged()
        {
            NetworkWriter writer = new NetworkWriter();

            long last    = 1;
            long current = 1; // unchanged

            // delta compress current against last
            DeltaCompression.Compress(writer, last, current);

            // nothing changed.
            // delta should compress it down a lot.
            Assert.That(writer.Position, Is.EqualTo(1));

            // decompress should get original result
            NetworkReader reader = new NetworkReader(writer);
            long decompressed = DeltaCompression.Decompress(reader, last);
            Assert.That(decompressed, Is.EqualTo(current));
        }

        [Test]
        public void Compress_Long_Changed()
        {
            NetworkWriter writer = new NetworkWriter();

            long last    = 1;
            long current = 7;

            // delta compress current against last
            DeltaCompression.Compress(writer, last, current);

            // two components = 8 bytes changed.
            // delta should compress it down a lot.
            Assert.That(writer.Position, Is.EqualTo(1));

            // decompress should get original result
            NetworkReader reader = new NetworkReader(writer);
            long decompressed = DeltaCompression.Decompress(reader, last);
            Assert.That(decompressed, Is.EqualTo(current));
        }

        [Test]
        public void Compress_Long_Changed_Large()
        {
            NetworkWriter writer = new NetworkWriter();

            long last    = 1;
            long current = 7000;

            // delta compress current against last
            DeltaCompression.Compress(writer, last, current);

            // two components = 8 bytes changed.
            // delta should compress it down a lot.
            // 7000 delta should use a few more bytes.
            Assert.That(writer.Position, Is.EqualTo(3));

            // decompress should get original result
            NetworkReader reader = new NetworkReader(writer);
            long decompressed = DeltaCompression.Decompress(reader, last);
            Assert.That(decompressed, Is.EqualTo(current));
        }

        [Test]
        public void Compress_Vector3Long_Unchanged()
        {
            NetworkWriter writer = new NetworkWriter();

            Vector3Long last    = new Vector3Long(1, 2, 3);
            Vector3Long current = new Vector3Long(1, 2, 3); // unchanged

            // delta compress current against last
            DeltaCompression.Compress(writer, last, current);

            // nothing changed.
            // delta should compress it down a lot.
            Assert.That(writer.Position, Is.EqualTo(3));

            // decompress should get original result
            NetworkReader reader = new NetworkReader(writer);
            Vector3Long decompressed = DeltaCompression.Decompress(reader, last);
            Assert.That(decompressed, Is.EqualTo(current));
        }

        [Test]
        public void Compress_Vector3Long_YZChanged()
        {
            NetworkWriter writer = new NetworkWriter();

            Vector3Long last    = new Vector3Long(1, 2, 3);
            Vector3Long current = new Vector3Long(1, 5, 7); // only 2 components change

            // delta compress current against last
            DeltaCompression.Compress(writer, last, current);

            // two components = 8 bytes changed.
            // delta should compress it down a lot.
            Assert.That(writer.Position, Is.EqualTo(3));

            // decompress should get original result
            NetworkReader reader = new NetworkReader(writer);
            Vector3Long decompressed = DeltaCompression.Decompress(reader, last);
            Assert.That(decompressed, Is.EqualTo(current));
        }

        [Test]
        public void Compress_Vector3Long_YZChanged_Large()
        {
            NetworkWriter writer = new NetworkWriter();

            Vector3Long last    = new Vector3Long(1, 2, 3);
            Vector3Long current = new Vector3Long(1, 5, 7000); // only 2 components change

            // delta compress current against last
            DeltaCompression.Compress(writer, last, current);

            // two components = 8 bytes changed.
            // delta should compress it down a lot.
            // 7000 delta should use a few more bytes.
            Assert.That(writer.Position, Is.EqualTo(5));

            // decompress should get original result
            NetworkReader reader = new NetworkReader(writer);
            Vector3Long decompressed = DeltaCompression.Decompress(reader, last);
            Assert.That(decompressed, Is.EqualTo(current));
        }

        [Test]
        public void Compress_Vector4Long_Unchanged()
        {
            NetworkWriter writer = new NetworkWriter();

            Vector4Long last    = new Vector4Long(1, 2, 3, 4);
            Vector4Long current = new Vector4Long(1, 2, 3, 4); // unchanged

            // delta compress current against last
            DeltaCompression.Compress(writer, last, current);

            // nothing changed.
            // delta should compress it down a lot.
            Assert.That(writer.Position, Is.EqualTo(4));

            // decompress should get original result
            NetworkReader reader = new NetworkReader(writer);
            Vector4Long decompressed = DeltaCompression.Decompress(reader, last);
            Assert.That(decompressed, Is.EqualTo(current));
        }

        [Test]
        public void Compress_Vector4Long_ZWChanged()
        {
            NetworkWriter writer = new NetworkWriter();

            Vector4Long last    = new Vector4Long(1, 2, 3, 4);
            Vector4Long current = new Vector4Long(1, 2, 7, 8); // only 2 components change

            // delta compress current against last
            DeltaCompression.Compress(writer, last, current);

            // two components = 8 bytes changed.
            // delta should compress it down a lot.
            Assert.That(writer.Position, Is.EqualTo(4));

            // decompress should get original result
            NetworkReader reader = new NetworkReader(writer);
            Vector4Long decompressed = DeltaCompression.Decompress(reader, last);
            Assert.That(decompressed, Is.EqualTo(current));
        }

        [Test]
        public void Compress_Vector4Long_ZWChanged_Large()
        {
            NetworkWriter writer = new NetworkWriter();

            Vector4Long last    = new Vector4Long(1, 2, 3, 4);
            Vector4Long current = new Vector4Long(1, 2, 5, 7000); // only 2 components change

            // delta compress current against last
            DeltaCompression.Compress(writer, last, current);

            // two components = 8 bytes changed.
            // delta should compress it down a lot.
            // 7000 delta should use a few more bytes.
            Assert.That(writer.Position, Is.EqualTo(6));

            // decompress should get original result
            NetworkReader reader = new NetworkReader(writer);
            Vector4Long decompressed = DeltaCompression.Decompress(reader, last);
            Assert.That(decompressed, Is.EqualTo(current));
        }
    }
}
