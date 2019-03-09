using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        // defining a SyncListStruct here will force Weaver to do work on this class
        // which will then force it to check for Server / Client guards and fail
        struct MyStruct
        {
            public int[][] jaggedArray;
            public float floatingpotato;
            public double givemetwopotatoes;
        }
        class MyStructClass : SyncListSTRUCT<MyStruct> {};
        MyStructClass Foo;
    }
}
