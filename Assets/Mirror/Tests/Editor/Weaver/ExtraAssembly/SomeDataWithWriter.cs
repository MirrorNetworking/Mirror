namespace Mirror.Weaver.Tests.Extra
{
    public struct SomeDataWithWriter
    {
        public int usefulNumber;
    }

    public static class ReadWrite
    {
        public static void WriteSomeData(this NetworkWriter writer, SomeDataWithWriter itemData)
        {
            writer.WriteInt(itemData.usefulNumber);
        }
        public static SomeDataWithWriter ReadSomeData(this NetworkReader reader)
        {
            return new SomeDataWithWriter { usefulNumber = reader.ReadInt() };
        }
    }
}
