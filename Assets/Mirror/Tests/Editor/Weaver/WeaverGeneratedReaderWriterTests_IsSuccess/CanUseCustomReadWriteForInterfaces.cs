using Mirror;

namespace GeneratedReaderWriter.CanUseCustomReadWriteForInterfaces
{
    public class CanUseCustomReadWriteForInterfaces : NetworkBehaviour
    {
        [ClientRpc]
        public void RpcDoSomething(IData data)
        {
            // empty
        }
    }

    public interface IData
    {
        int id { get; }
    }

    public class SomeData : IData
    {
        public int id => 1;
    }

    public static class DataReadWrite
    {
        public static void WriteData(this NetworkWriter writer, IData data)
        {
            writer.WriteInt(data.id);
            // write extra stuff depending on id here
        }

        public static IData ReadData(this NetworkReader reader)
        {
            int id = reader.ReadInt();
            // do something with id

            SomeData someData = new SomeData();
            // read extra stuff depending on id here

            return someData;
        }
    }

}
