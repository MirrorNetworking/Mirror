// simple component that holds team information
using System;
using UnityEngine;

namespace Mirror
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/ Interest Management/ Team/Network Team")]
    [HelpURL("https://mirror-networking.gitbook.io/docs/guides/interest-management")]
    public class NetworkTeam : NetworkBehaviour
    {
        [SerializeField]
        [Tooltip("Set teamId on Server at runtime to the same value on all networked objects that belong to a given team")]
        string _teamId;

        public string teamId
        {
            get => _teamId;
            set
            {
                if (Application.IsPlaying(gameObject) && !NetworkServer.active)
                    throw new InvalidOperationException("teamId can only be set at runtime on active server");

                if (_teamId == value)
                    return;

                string oldTeam = _teamId;
                _teamId = value;

                //Only inform the AOI if this netIdentity has been spawned(isServer) and only if using a TeamInterestManagement
                if (isServer && NetworkServer.aoi is TeamInterestManagement teamInterestManagement)
                    teamInterestManagement.OnTeamChanged(this, oldTeam);
            }
        }

        [Tooltip("When enabled this object is visible to all clients. Typically this would be true for player objects")]
        public bool forceShown;
    }
}
