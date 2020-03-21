using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        [Client]
        void ClientOnlyMethod() 
        {
            // test method
        }
    }
}
