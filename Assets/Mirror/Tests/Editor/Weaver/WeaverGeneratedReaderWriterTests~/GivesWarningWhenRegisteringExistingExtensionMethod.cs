using Mirror;

namespace GeneratedReaderWriter.GivesWarningWhenRegisteringExistingExtensionMethod
{
    public struct MyType
    {
        public int number;
    }

    public static class ReadWriteExtension
    {
        public static void WriteMyType(this NetworkWriter writer, MyType data)
        {
            writer.WriteInt(data.number);
        }

        public static void WriteMyType2(this NetworkWriter writer, MyType data)
        {
            writer.WriteInt(data.number);
        }

        public static MyType ReadMyType(this NetworkReader reader)
        {
            return new MyType { number = reader.ReadInt() };
        }

        public static MyType ReadMyType2(this NetworkReader reader)
        {
            return new MyType { number = reader.ReadInt() };
        }
    }
}
