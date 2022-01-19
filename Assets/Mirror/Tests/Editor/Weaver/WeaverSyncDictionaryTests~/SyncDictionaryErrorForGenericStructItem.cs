using Mirror;

namespace WeaverSyncDictionaryTests.SyncDictionaryErrorForGenericStructItem
{
    class SyncDictionaryErrorForGenericStructItem : NetworkBehaviour
    {
        struct MyGenericStruct<T>
        {
            T genericpotato;
        }

        class MyGenericStructDictionary : SyncDictionary<int, MyGenericStruct<float>> { };

        readonly MyGenericStructDictionary harpseals;
    }

}
