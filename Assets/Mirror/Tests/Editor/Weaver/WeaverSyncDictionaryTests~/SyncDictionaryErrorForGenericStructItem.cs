using Mirror;

namespace Mirror.Weaver.Tests.SyncDictionaryErrorForGenericStructItem
{
    class SyncDictionaryErrorForGenericStructItem : NetworkBehaviour
    {
         struct MyGenericStruct<T>
        {
            T genericpotato;
        }

        class MyGenericStructDictionary : SyncDictionary<int, MyGenericStruct<float>> { };
        
        MyGenericStructDictionary harpseals;
    }
   
}
