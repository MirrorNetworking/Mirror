using Mirror;

namespace MirrorTest
{
    class SyncDictionaryErrorForGenericStructKey : NetworkBehaviour
    {
        MyGenericStructDictionary harpseals;


        struct MyGenericStruct<T>
        {
            T genericpotato;
        }

        class MyGenericStructDictionary : SyncDictionary<MyGenericStruct<float>, int> { };
    }
}
