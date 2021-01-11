using NUnit.Framework;

namespace Mirror.Tests
{
    public struct TestMessage
    {
        public int IntValue;
        public string StringValue;
        public double DoubleValue;
    }

    public class ClassWithoutBaseMessage
    {
        public int[] array;
    }

    public abstract class AbstractMessage
    {
    }

    public class OverrideMessage : AbstractMessage
    {
        public int someValue;
    }

    public class Layer1Message
    {
        public int value1;
    }

    public class Layer2Message : Layer1Message
    {
        public int value2;
    }

    public class Layer3Message : Layer2Message
    {
        public int value3;
    }

    [TestFixture]
    public class MessageBaseTests
    {
        [Test]
        public void StructWithMethods()
        {
            byte[] arr = MessagePacker.Pack(new TestMessage
            {
                IntValue = 1,
                DoubleValue = 3.3,
                StringValue = "2"
            });
            TestMessage t = MessagePacker.Unpack<TestMessage>(arr);

            Assert.AreEqual(1, t.IntValue);
        }

        [Test]
        public void ClassWithEmptyMethods()
        {
            var intMessage = new ClassWithoutBaseMessage
            {
                array = new[] { 3, 4, 5 }
            };

            byte[] data = MessagePacker.Pack(intMessage);

            ClassWithoutBaseMessage unpacked = MessagePacker.Unpack<ClassWithoutBaseMessage>(data);

            Assert.That(unpacked.array, Is.EquivalentTo(new [] { 3, 4, 5 }));
        }

        [Test]
        public void AbstractBaseClassWorks()
        {
            const int value = 10;
            var intMessage = new OverrideMessage
            {
                someValue = value
            };

            byte[] data = MessagePacker.Pack(intMessage);

            OverrideMessage unpacked = MessagePacker.Unpack<OverrideMessage>(data);

            Assert.That(unpacked.someValue, Is.EqualTo(value));
        }

        [Test]
        public void MessageInheirtanceWorksWithMultipleLayers()
        {
            const int value1 = 10;
            const int value2 = 13;
            const int value3 = 15;
            var intMessage = new Layer3Message
            {
                value1 = value1,
                value2 = value2,
                value3 = value3
            };

            byte[] data = MessagePacker.Pack(intMessage);

            Layer3Message unpacked = MessagePacker.Unpack<Layer3Message>(data);

            Assert.That(unpacked.value1, Is.EqualTo(value1));
            Assert.That(unpacked.value2, Is.EqualTo(value2));
            Assert.That(unpacked.value3, Is.EqualTo(value3));
        }
    }
}
