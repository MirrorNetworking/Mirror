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
        public enum MyByteEnum : byte
        {
            A, B, C, D
        }

        public enum MyShortEnum : short
        {
            E, F, G, H
        }

        public enum MyCustomEnum
        {
            M, N, O, P
        }


        [Test]
        public void ByteIsSentForByteEnum()
        {
            MyByteEnum byteEnum = MyByteEnum.B;

            var writer = new NetworkWriter();
            writer.Write(byteEnum);

            // should only be 1 byte
            Assert.That(writer.Length, Is.EqualTo(1));
        }

        [Test]
        public void ShortIsSentForShortEnum()
        {
            MyShortEnum shortEnum = MyShortEnum.G;

            var writer = new NetworkWriter();
            writer.Write(shortEnum);

            // should only be 1 byte
            Assert.That(writer.Length, Is.EqualTo(2));
        }

        [Test]
        public void CustomWriterIsUsedForEnum()
        {
            MyCustomEnum customEnum = MyCustomEnum.O;
            MyCustomEnum clientMsg = SerializeAndDeserializeMessage(customEnum);

            // custom writer should write N if it sees O
            Assert.That(clientMsg, Is.EqualTo(MyCustomEnum.N));
        }

        T SerializeAndDeserializeMessage<T>(T msg)
        {
            var writer = new NetworkWriter();
            writer.Write(msg);

            var reader = new NetworkReader(writer.ToArraySegment());
            return reader.Read<T>();
        }
    }
}
