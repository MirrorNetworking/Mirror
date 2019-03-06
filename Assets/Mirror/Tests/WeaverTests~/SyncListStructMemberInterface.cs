using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        interface IPotato
        {
            void Bake();
        }

        struct MyStruct
        {
            public IPotato potato;
        }

        class MyStructClass : SyncListSTRUCT<MyStruct> {};

        MyStructClass harpseals;
    }
}
