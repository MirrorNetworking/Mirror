using Mirror;

namespace WeaverSyncDictionaryTests.SyncDictionaryGenericStructItemWithCustomMethods
{
    class SyncDictionaryGenericStructItemWithCustomMethods : NetworkBehaviour
    {
        readonly SyncDictionary<int, MyGenericStruct<float>> harpseals;
    }

    public struct MyGenericStruct<T>
    {
        public T genericpotato;
    }

    public static class MyGenericStructDictionary
    {
        public static void WriteItem(this NetworkWriter writer, MyGenericStruct<float> item)
        {
            writer.WriteFloat(item.genericpotato);
        }

        public static MyGenericStruct<float> ReadItem(this NetworkReader reader)
        {
            return new MyGenericStruct<float>() { genericpotato = reader.ReadFloat() };
        }
    }
}
