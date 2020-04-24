using Mirror;

namespace MirrorTest
{
    class SyncDictionaryErrorForGenericStructItemWithCustomSerializeOnly : NetworkBehaviour
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
        }
    }
}
