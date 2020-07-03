using Mirror;

namespace WeaverSyncDictionaryTests.SyncDictionaryGenericStructItemWithCustomMethods
{
    class SyncDictionaryGenericStructItemWithCustomMethods : NetworkBehaviour
    {
        MyGenericStructDictionary harpseals;


        struct MyGenericStruct<T>
        {
            public T genericpotato;
        }

        class MyGenericStructDictionary : SyncDictionary<int, MyGenericStruct<float>>
        {
            protected override void SerializeItem(NetworkWriter writer, MyGenericStruct<float> item)
            {
                writer.WriteSingle(item.genericpotato);
            }

            protected override MyGenericStruct<float> DeserializeItem(NetworkReader reader)
            {
                return new MyGenericStruct<float>() { genericpotato = reader.ReadSingle() };
            }
        }
    }
}
