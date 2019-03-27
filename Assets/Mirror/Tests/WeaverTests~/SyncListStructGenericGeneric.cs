using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        struct MyPODStruct
        {
            float floatingpotato;
        }

        struct MyGenericStruct<T>
        {
            T genericpotato;
        }

        struct MyStruct
        {
            MyGenericStruct<MyPODStruct> potato;
        }

        class MyStructClass : SyncList<MyGenericStruct<float>> {};

        MyStructClass harpseals;
    }
}
