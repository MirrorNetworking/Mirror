using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer
    {
        // defining a SyncListStruct here will force Weaver to do work on this class
        // which will then force it to check for Server / Client guards and fail
        struct MyStruct
        {
            int potato;
            float floatingpotato;
            double givemetwopotatoes;
        }
        class MyStructClass : SyncListSTRUCT<MyStruct> {};
        MyStructClass Foo;

        [Server]
        public void CantServerGuardInThisClass() {}

        [ServerCallback]
        public void CantServerCallbackGuardInThisClass() {}

        [Client]
        public void CantClientGuardInThisClass() {}

        [ClientCallback]
        public void CantClientCallbackGuardInThisClass() {}
    }
}
