using Mirror;

namespace WeaverSyncDictionaryTests.SyncDictionaryGenericStructKeyWithCustomMethods
{
    class SyncDictionaryGenericStructKeyWithCustomMethods : NetworkBehaviour
    {
        MyGenericStructDictionary harpseals;


        struct MyGenericStruct<T>
        {
            public T genericpotato;
        }

        class MyGenericStructDictionary : SyncDictionary<MyGenericStruct<float>, int>
        {
            protected override void SerializeKey(NetworkWriter writer, MyGenericStruct<float> item)
            {
                writer.WriteSingle(item.genericpotato);
            }

            protected override MyGenericStruct<float> DeserializeKey(NetworkReader reader)
            {
                return new MyGenericStruct<float>() { genericpotato = reader.ReadSingle() };
            }
        }
    }
}
