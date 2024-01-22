// simple component that holds match information
using System;
using UnityEngine;

namespace Mirror
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/ Interest Management/ Match/Network Match")]
    [HelpURL("https://mirror-networking.gitbook.io/docs/guides/interest-management")]
    public class NetworkMatch : NetworkBehaviour
    {
        Guid _matchId;

#pragma warning disable IDE0052 // Suppress warning for unused field...this is for debugging purposes
        [SerializeField, ReadOnly]
        [Tooltip("Match ID is shown here on server for debugging purposes.")]
        string MatchID = string.Empty;
#pragma warning restore IDE0052

        ///<summary>Set this to the same value on all networked objects that belong to a given match</summary>
        public Guid matchId
        {
            get => _matchId;
            set
            {
                if (!NetworkServer.active)
                    throw new InvalidOperationException("matchId can only be set at runtime on active server");

                if (_matchId == value)
                    return;

                Guid oldMatch = _matchId;
                _matchId = value;
                MatchID = value.ToString();

                // Only inform the AOI if this netIdentity has been spawned (isServer) and only if using a MatchInterestManagement
                if (isServer && NetworkServer.aoi is MatchInterestManagement matchInterestManagement)
                    matchInterestManagement.OnMatchChanged(this, oldMatch);
            }
        }
    }
}
