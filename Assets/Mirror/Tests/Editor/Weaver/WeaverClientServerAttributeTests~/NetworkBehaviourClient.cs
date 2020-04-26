using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace Mirror.Weaver.Tests.NetworkBehaviourClient
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
