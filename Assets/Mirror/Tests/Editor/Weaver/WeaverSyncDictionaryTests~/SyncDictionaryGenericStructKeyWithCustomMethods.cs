using Mirror;

namespace WeaverSyncDictionaryTests.SyncDictionaryGenericStructKeyWithCustomMethods
{
    class SyncDictionaryGenericStructKeyWithCustomMethods : NetworkBehaviour
    {
        readonly SyncDictionary<MyGenericStruct<float>, int> harpseals;
    }

    public struct MyGenericStruct<T>
    {
        public T genericpotato;
    }

    public static class MyGenericStructDictionary
    {
        public static void WriteKey(this NetworkWriter writer, MyGenericStruct<float> item)
        {
            writer.WriteFloat(item.genericpotato);
        }

        public static MyGenericStruct<float> ReadKey(this NetworkReader reader)
        {
            return new MyGenericStruct<float>() { genericpotato = reader.ReadFloat() };
        }
    }
}
