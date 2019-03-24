using System;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        class NonBasicPotato
        {
            int potato;
        }

        struct MyStruct
        {
            public object nonbasicpotato;
        }

        class MyStructClass : SyncList<MyStruct>
        {
            int potatoCount;
            public MyStructClass(int numberOfPotatoes)
            {
                potatoCount = numberOfPotatoes;
            }
        };

        MyStructClass harpseals;
    }
}
