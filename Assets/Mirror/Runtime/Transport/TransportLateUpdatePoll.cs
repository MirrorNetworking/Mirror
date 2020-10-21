using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// Checks for events and invokes them in LateUpdate
    /// </summary>
    public class TransportLateUpdatePoll : MonoBehaviour
    {
        [SerializeField] ICommonTransport[] transports;

        public void LateUpdate()
        {
            foreach (ICommonTransport transport in transports)
            {
                transport.CheckForEvents();
            }
        }
    }
}
