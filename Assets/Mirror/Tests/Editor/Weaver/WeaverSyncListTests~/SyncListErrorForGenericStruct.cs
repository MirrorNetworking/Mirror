using Mirror;

namespace SyncListErrorForGenericStruct
{
    class MyBehaviour : NetworkBehaviour
    {
        MyGenericStructList harpseals;
    }

    struct MyGenericStruct<T>
    {
        T genericpotato;
    }

    class MyGenericStructList : SyncList<MyGenericStruct<float>> { };
}
