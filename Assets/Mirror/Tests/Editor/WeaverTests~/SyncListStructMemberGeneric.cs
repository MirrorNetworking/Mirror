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
            public MyGenericStruct<float> potato;
        }

        class MyStructClass : SyncList<MyStruct> {};

        MyStructClass harpseals;
    }
}
