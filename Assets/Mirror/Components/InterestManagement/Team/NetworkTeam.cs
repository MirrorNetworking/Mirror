// simple component that holds team information
using UnityEngine;

namespace Mirror
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkTeam")]
    [HelpURL("https://mirror-networking.gitbook.io/docs/guides/interest-management")]
    public class NetworkTeam : NetworkBehaviour
    {
        [Tooltip("Set this to the same value on all networked objects that belong to a given team")]
        public string teamId = string.Empty;

        [Tooltip("When enabled this object is visible to all clients. Typically this would be true for player objects")]
        public bool forceShown;
    }
}
