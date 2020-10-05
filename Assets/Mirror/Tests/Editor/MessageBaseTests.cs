using NUnit.Framework;

namespace Mirror.Tests.MessageTests
{
    struct TestMessage : NetworkMessage
    {
        public int IntValue;
        public string StringValue;
        public double DoubleValue;

        public TestMessage(int i, string s, double d)
        {
            IntValue = i;
            StringValue = s;
            DoubleValue = d;
        }

        public void Deserialize(NetworkReader reader)
        {
            IntValue = reader.ReadInt32();
            StringValue = reader.ReadString();
            DoubleValue = reader.ReadDouble();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WriteInt32(IntValue);
            writer.WriteString(StringValue);
            writer.WriteDouble(DoubleValue);
        }
    }

    struct StructWithEmptyMethodMessage : NetworkMessage
    {
        public int IntValue;
        public string StringValue;
        public double DoubleValue;

        // Mirror will fill out these empty methods
        public void Deserialize(NetworkReader reader) { }
        public void Serialize(NetworkWriter writer) { }
    }

    class ClassWithoutBaseMessage : NetworkMessage
    {
        public int[] array;

        // Mirror will fill out these empty methods
        public void Deserialize(NetworkReader reader) { }
        public void Serialize(NetworkWriter writer) { }
    }

    abstract class AbstractMessage : NetworkMessage
    {
        public abstract void Deserialize(NetworkReader reader);
        public abstract void Serialize(NetworkWriter writer);
    }

    class OverrideMessage : AbstractMessage
    {
        public int someValue;

        // Mirror will fill out these empty methods
        public override void Serialize(NetworkWriter writer) { }
        public override void Deserialize(NetworkReader reader) { }
    }

    class Layer1Message : NetworkMessage
    {
        public int value1;

        // Mirror will fill out these empty methods
        public virtual void Serialize(NetworkWriter writer) { }
        public virtual void Deserialize(NetworkReader reader) { }
    }

    class Layer2Message : Layer1Message
    {
        public int value2;
    }
    class Layer3Message : Layer2Message
    {
        public int value3;
    }

    class NullableObject
    {
        public int id = 3;
    }

    class NullableObjectMessage : NetworkMessage
    {
        public string name;
        public NullableObject nullObj;
    }

    [TestFixture]
    public class MessageBaseTests
    {
        [Test]
        public void StructWithMethods()
        {
            byte[] arr = MessagePackerTest.PackToByteArray(new TestMessage(1, "2", 3.3));
            TestMessage t = MessagePacker.Unpack<TestMessage>(arr);

            Assert.AreEqual(1, t.IntValue);
        }

        [Test]
        public void StructWithEmptyMethods()
        {
            byte[] arr = MessagePackerTest.PackToByteArray(new StructWithEmptyMethodMessage { IntValue = 1, StringValue = "2", DoubleValue = 3.3 });
            StructWithEmptyMethodMessage t = MessagePacker.Unpack<StructWithEmptyMethodMessage>(arr);

            Assert.AreEqual(1, t.IntValue);
            Assert.AreEqual("2", t.StringValue);
            Assert.AreEqual(3.3, t.DoubleValue);
        }

        [Test]
        public void ClassWithEmptyMethods()
        {
            ClassWithoutBaseMessage intMessage = new ClassWithoutBaseMessage
            {
                array = new[] { 3, 4, 5 }
            };

            byte[] data = MessagePackerTest.PackToByteArray(intMessage);

            ClassWithoutBaseMessage unpacked = MessagePacker.Unpack<ClassWithoutBaseMessage>(data);

            Assert.That(unpacked.array, Is.EquivalentTo(new int[] { 3, 4, 5 }));
        }

        [Test]
        public void AbstractBaseClassWorks()
        {
            const int value = 10;
            OverrideMessage intMessage = new OverrideMessage
            {
                someValue = value
            };

            byte[] data = MessagePackerTest.PackToByteArray(intMessage);

            OverrideMessage unpacked = MessagePacker.Unpack<OverrideMessage>(data);

            Assert.That(unpacked.someValue, Is.EqualTo(value));
        }

        [Test]
        public void MessageInheirtanceWorksWithMultipleLayers()
        {
            const int value1 = 10;
            const int value2 = 13;
            const int value3 = 15;
            Layer3Message intMessage = new Layer3Message
            {
                value1 = value1,
                value2 = value2,
                value3 = value3
            };

            byte[] data = MessagePackerTest.PackToByteArray(intMessage);

            Layer3Message unpacked = MessagePacker.Unpack<Layer3Message>(data);

            Assert.That(unpacked.value1, Is.EqualTo(value1));
            Assert.That(unpacked.value2, Is.EqualTo(value2));
            Assert.That(unpacked.value3, Is.EqualTo(value3));
        }

        [Test]
        public void NullObjectMessageTest()
        {
            NullableObjectMessage nullableObjectMessage = new NullableObjectMessage
            {
                name = "pepe",
                nullObj = null
            };

            byte[] data = MessagePackerTest.PackToByteArray(nullableObjectMessage);

            NullableObjectMessage unpacked = MessagePacker.Unpack<NullableObjectMessage>(data);

            Assert.That(unpacked.name, Is.EqualTo("pepe"));
            Assert.That(unpacked.nullObj, Is.Null);
        }
    }
}
