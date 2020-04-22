using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace MirrorTest
{
    class NetworkBehaviourClient : NetworkBehaviour
    {
        [Client]
        void ClientOnlyMethod() 
        {
            // test method
        }
    }
}
