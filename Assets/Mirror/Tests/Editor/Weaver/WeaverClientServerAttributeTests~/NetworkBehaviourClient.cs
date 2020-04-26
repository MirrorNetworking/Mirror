using System;
using System.Collections;
using UnityEngine;
using Mirror;

namespace WeaverClientServerAttributeTests.NetworkBehaviourClient
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
