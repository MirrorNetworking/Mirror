using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverClientServerAttributeTests.NetworkBehaviourServer
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
