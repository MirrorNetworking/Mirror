using Mirror;

namespace MirrorTest
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
