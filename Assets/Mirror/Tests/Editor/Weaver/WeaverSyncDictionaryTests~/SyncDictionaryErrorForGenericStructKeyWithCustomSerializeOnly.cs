using Mirror;

namespace MirrorTest
{
    class SyncDictionaryErrorForGenericStructKeyWithCustomSerializeOnly : NetworkBehaviour
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
        }
    }
}
