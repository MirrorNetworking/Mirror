using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// Checks for events and invokes them in LateUpdate
    /// </summary>
    public class TransportLateUpdatePoll : MonoBehaviour
    {
        public List<ICommonTransport> transports;

        public void LateUpdate()
        {
            foreach (ICommonTransport transport in transports)
            {
                transport.CheckForEvents();
            }
        }
    }
}
