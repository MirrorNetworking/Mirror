using Mirror;

namespace WeaverSyncDictionaryTests.SyncDictionaryErrorForGenericStructItemWithCustomDeserializeOnly
{
    class SyncDictionaryErrorForGenericStructItemWithCustomDeserializeOnly : NetworkBehaviour
    {
        MyGenericStructDictionary harpseals;


        struct MyGenericStruct<T>
        {
            public T genericpotato;
        }

        class MyGenericStructDictionary : SyncDictionary<int, MyGenericStruct<float>>
        {
            protected override MyGenericStruct<float> DeserializeItem(NetworkReader reader)
            {
                return new MyGenericStruct<float>() { genericpotato = reader.ReadSingle() };
            }
        }
    }
}
