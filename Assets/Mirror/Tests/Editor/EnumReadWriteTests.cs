using NUnit.Framework;

namespace Mirror.Tests
{
    public static class MyCustomEnumReadWrite
    {
        public static void WriteMyCustomEnum(this NetworkWriter networkWriter, EnumReadWriteTests.MyCustomEnum customEnum)
        {
            // if O write N
            if (customEnum == EnumReadWriteTests.MyCustomEnum.O)
            {
                networkWriter.WriteInt32((int)EnumReadWriteTests.MyCustomEnum.N);
            }
            else
            {
                networkWriter.WriteInt32((int)customEnum);
            }
        }
        public static EnumReadWriteTests.MyCustomEnum ReadMyCustomEnum(this NetworkReader networkReader)
        {
            return (EnumReadWriteTests.MyCustomEnum)networkReader.ReadInt32();
        }
    }
    public class EnumReadWriteTests
    {
        public class ByteMessage : MessageBase { public MyByteEnum byteEnum; }
        public enum MyByteEnum : byte
        {
            A, B, C, D
        }

        public class ShortMessage : MessageBase { public MyShortEnum shortEnum; }
        public enum MyShortEnum : short
        {
            E, F, G, H
        }

        public class CustomMessage : MessageBase { public MyCustomEnum customEnum; }

        public enum MyCustomEnum
        {
            M, N, O, P
        }


        [Test]
        public void ByteIsSentForByteEnum()
        {
            ByteMessage msg = new ByteMessage() { byteEnum = MyByteEnum.B };

            NetworkWriter writer = new NetworkWriter();
            msg.Serialize(writer);

            // should only be 1 byte
            Assert.That(writer.Length, Is.EqualTo(1));
        }

        [Test]
        public void ShortIsSentForShortEnum()
        {
            ShortMessage msg = new ShortMessage() { shortEnum = MyShortEnum.G };

            NetworkWriter writer = new NetworkWriter();
            msg.Serialize(writer);

            // should only be 1 byte
            Assert.That(writer.Length, Is.EqualTo(2));
        }

        [Test]
        public void CustomWriterIsUsedForEnum()
        {
            CustomMessage serverMsg = new CustomMessage() { customEnum = MyCustomEnum.O };
            CustomMessage clientMsg = SerializeAndDeserializeMessage(serverMsg);

            // custom writer should write N if it sees O
            Assert.That(clientMsg.customEnum, Is.EqualTo(MyCustomEnum.N));
        }
        T SerializeAndDeserializeMessage<T>(T msg) where T : MessageBase, new()
        {
            NetworkWriter writer = new NetworkWriter();

            msg.Serialize(writer);

            NetworkReader reader = new NetworkReader(writer.ToArraySegment());

            T msg2 = new T();
            msg2.Deserialize(reader);
            return msg2;
        }
    }
}
