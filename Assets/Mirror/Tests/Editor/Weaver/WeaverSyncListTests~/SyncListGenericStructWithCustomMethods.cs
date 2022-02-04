using Mirror;

namespace WeaverSyncListTests.SyncListGenericStructWithCustomMethods
{
    class SyncListGenericStructWithCustomMethods : NetworkBehaviour
    {
        readonly SyncList<MyGenericStruct<float>> harpseals;
    }

    struct MyGenericStruct<T>
    {
        public T genericpotato;
    }

    static class MyGenericStructList
    {
        static void SerializeItem(this NetworkWriter writer, MyGenericStruct<float> item)
        {
            writer.WriteFloat(item.genericpotato);
        }

        static MyGenericStruct<float> DeserializeItem(this NetworkReader reader)
        {
            return new MyGenericStruct<float>() { genericpotato = reader.ReadFloat() };
        }
    }
}
