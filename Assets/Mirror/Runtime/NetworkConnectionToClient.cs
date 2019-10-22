using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public class NetworkConnectionToClient : NetworkConnection
    {
        public NetworkConnectionToClient(string networkAddress, int networkConnectionId) : base(networkAddress, networkConnectionId)
        {
        }
    }
}