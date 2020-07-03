using Mirror;

namespace WeaverSyncListTests.SyncListErrorForGenericStruct
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
