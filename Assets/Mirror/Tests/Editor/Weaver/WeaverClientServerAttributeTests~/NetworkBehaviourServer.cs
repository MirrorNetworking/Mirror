using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourServer : NetworkBehaviour
    {
        [Server]
        void ServerOnlyMethod() 
        {
            // test method
        }
    }
}
