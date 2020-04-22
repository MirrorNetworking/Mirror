using Mirror;

namespace MirrorTest
{
    class SyncDictionaryErrorForGenericStructItem : NetworkBehaviour
    {
        MyGenericStructDictionary harpseals;
    }

    struct MyGenericStruct<T>
    {
        T genericpotato;
    }

    class MyGenericStructDictionary : SyncDictionary<int, MyGenericStruct<float>> { };
}
