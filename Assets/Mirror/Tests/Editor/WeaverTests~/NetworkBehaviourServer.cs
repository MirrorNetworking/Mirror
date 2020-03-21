using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class MirrorTestPlayer : NetworkBehaviour
    {
        [Server]
        void ServerOnlyMethod() 
        {
            // test method
        }
    }
}
