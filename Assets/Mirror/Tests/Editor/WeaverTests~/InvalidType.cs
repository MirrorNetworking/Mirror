using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        struct MyStruct
        {
            public AccessViolationException violatedPotato;
        }

        class MyStructClass : SyncList<MyStruct> {};

        MyStructClass harpseals;
    }
}
