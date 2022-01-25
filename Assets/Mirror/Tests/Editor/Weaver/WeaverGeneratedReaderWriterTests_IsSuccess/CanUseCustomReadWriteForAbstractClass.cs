using Mirror;

namespace GeneratedReaderWriter.CanUseCustomReadWriteForAbstractClass
{
    public class CanUseCustomReadWriteForAbstractClass : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(DataBase data)
        {
            // empty
        }
    }

    public abstract class DataBase
    {
        public int someField;
        public abstract int id { get; }
    }

    public class SomeData : DataBase
    {
        public float anotherField;
        public override int id => 1;
    }

    public static class DataReadWrite
    {
        public static void WriteData(this NetworkWriter writer, DataBase data)
        {
            writer.WriteInt(data.id);
            // write extra stuff depending on id here
            writer.WriteInt(data.someField);

            if (data.id == 1)
            {
                SomeData someData = (SomeData)data;
                writer.WriteFloat(someData.anotherField);
            }
        }

        public static DataBase ReadData(this NetworkReader reader)
        {
            int id = reader.ReadInt();

            int someField = reader.ReadInt();
            DataBase data = null;
            if (data.id == 1)
            {
                SomeData someData = new SomeData()
                {
                    someField = someField
                };
                // read extra stuff depending on id here

                someData.anotherField = reader.ReadFloat();

                data = someData;
            }
            return data;
        }
    }
}
