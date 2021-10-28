// simple component that holds team information
using System;
using UnityEngine;

namespace Mirror
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkTeam")]
    [HelpURL("https://mirror-networking.gitbook.io/docs/guides/interest-management")]
    public class NetworkTeam : NetworkBehaviour
    {
        ///<summary>Set this to the same value on all networked objects that belong to a given team</summary>
        public string teamId = string.Empty;
    }
}
