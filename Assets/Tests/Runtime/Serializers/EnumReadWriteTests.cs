using NUnit.Framework;

namespace Mirror.Tests
{
    public static class MyCustomEnumReadWrite
    {
        public static void WriteMyCustomEnum(this NetworkWriter networkWriter, EnumReadWriteTests.MyCustom customEnum)
        {
            // if O write N
            if (customEnum == EnumReadWriteTests.MyCustom.O)
            {
                networkWriter.WriteInt32((int)EnumReadWriteTests.MyCustom.N);
            }
            else
            {
                networkWriter.WriteInt32((int)customEnum);
            }
        }
        public static EnumReadWriteTests.MyCustom ReadMyCustomEnum(this NetworkReader networkReader)
        {
            return (EnumReadWriteTests.MyCustom)networkReader.ReadInt32();
        }
    }
    public class EnumReadWriteTests
    {
        public enum MyByte : byte
        {
            A, B, C, D
        }

        public enum MyShort : short
        {
            E, F, G, H
        }

        public enum MyCustom
        {
            M, N, O, P
        }


        [Test]
        public void ByteIsSentForByteEnum()
        {
            MyByte byteEnum = MyByte.B;

            var writer = new NetworkWriter();
            writer.Write(byteEnum);

            // should only be 1 byte
            Assert.That(writer.Length, Is.EqualTo(1));
        }

        [Test]
        public void ShortIsSentForShortEnum()
        {
            MyShort shortEnum = MyShort.G;

            var writer = new NetworkWriter();
            writer.Write(shortEnum);

            // should only be 1 byte
            Assert.That(writer.Length, Is.EqualTo(2));
        }

        [Test]
        public void CustomWriterIsUsedForEnum()
        {
            MyCustom customEnum = MyCustom.O;
            MyCustom clientMsg = SerializeAndDeserializeMessage(customEnum);

            // custom writer should write N if it sees O
            Assert.That(clientMsg, Is.EqualTo(MyCustom.N));
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
