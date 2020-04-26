using Mirror;

namespace Mirror.Weaver.Tests.SyncListErrorForGenericStruct
{
    class SyncListErrorForGenericStruct : NetworkBehaviour
    {
        MyGenericStructList harpseals;
    

        struct MyGenericStruct<T>
        {
            T genericpotato;
        }

        class MyGenericStructList : SyncList<MyGenericStruct<float>> { };
    }
}
