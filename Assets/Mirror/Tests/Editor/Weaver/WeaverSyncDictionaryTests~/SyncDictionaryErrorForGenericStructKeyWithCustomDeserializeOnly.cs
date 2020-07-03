using Mirror;

namespace WeaverSyncDictionaryTests.SyncDictionaryErrorForGenericStructKeyWithCustomDeserializeOnly
{
    class SyncDictionaryErrorForGenericStructKeyWithCustomDeserializeOnly : NetworkBehaviour
    {
        MyGenericStructDictionary harpseals;


        struct MyGenericStruct<T>
        {
            public T genericpotato;
        }

        class MyGenericStructDictionary : SyncDictionary<MyGenericStruct<float>, int>
        {
            protected override MyGenericStruct<float> DeserializeKey(NetworkReader reader)
            {
                return new MyGenericStruct<float>() { genericpotato = reader.ReadSingle() };
            }
        }
    }
}
