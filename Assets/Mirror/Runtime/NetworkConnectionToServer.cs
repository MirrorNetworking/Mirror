using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public class NetworkConnectionToServer : NetworkConnection
    {
        public NetworkConnectionToServer(string networkAddress) : base(networkAddress)
        {
        }
    }

}