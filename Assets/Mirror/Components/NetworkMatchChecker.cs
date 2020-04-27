using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// Component that controls visibility of networked objects based on match id.
    /// <para>Any object with this component on it will only be visible to other objects in the same match.</para>
    /// <para>This would be used to isolate players to their respective matches within a single game server instance. </para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkMatchChecker")]
    [RequireComponent(typeof(NetworkIdentity))]
    [HelpURL("https://mirror-networking.com/docs/Components/NetworkMatchChecker.html")]
    public class NetworkMatchChecker : NetworkVisibility
    {
        static readonly Dictionary<Guid, HashSet<NetworkIdentity>> matchPlayers = new Dictionary<Guid, HashSet<NetworkIdentity>>();

        Guid currentMatch = Guid.Empty;

        [Header("Diagnostics")]
        [SyncVar]
        public string currentMatchDebug;

        /// <summary>
        /// Set this to the same value on all networked objects that belong to a given match
        /// </summary>
        public Guid matchId
        {
            get { return currentMatch; }
            set
            {
                if (currentMatch == value) return;

                // cache previous match so observers in that match can be rebuilt
                Guid previousMatch = currentMatch;

                // Set this to the new match this object just entered ...
                currentMatch = value;
                // ... and copy the string for the inspector because Unity can't show Guid directly
                currentMatchDebug = currentMatch.ToString();

                if (previousMatch != Guid.Empty)
                {
                    // Remove this object from the hashset of the match it just left
                    matchPlayers[previousMatch].Remove(netIdentity);

                    // RebuildObservers of all NetworkIdentity's in the match this object just left
                    RebuildMatchObservers(previousMatch);
                }

                if (currentMatch != Guid.Empty)
                {
                    // Make sure this new match is in the dictionary
                    if (!matchPlayers.ContainsKey(currentMatch))
                        matchPlayers.Add(currentMatch, new HashSet<NetworkIdentity>());

                    // Add this object to the hashset of the new match
                    matchPlayers[currentMatch].Add(netIdentity);

                    // RebuildObservers of all NetworkIdentity's in the match this object just entered
                    RebuildMatchObservers(currentMatch);
                }
                else
                {
                    // Not in any match now...RebuildObservers will clear and add self
                    netIdentity.RebuildObservers(false);
                }
            }
        }

        public override void OnStartServer()
        {
            if (currentMatch == Guid.Empty) return;

            if (!matchPlayers.ContainsKey(currentMatch))
                matchPlayers.Add(currentMatch, new HashSet<NetworkIdentity>());

            matchPlayers[currentMatch].Add(netIdentity);

            // No need to rebuild anything here.
            // identity.RebuildObservers is called right after this from NetworkServer.SpawnObject
        }

        void RebuildMatchObservers(Guid specificMatch)
        {
            foreach (NetworkIdentity networkIdentity in matchPlayers[specificMatch])
                if (networkIdentity != null)
                    networkIdentity.RebuildObservers(false);
        }

        #region Observers

        /// <summary>
        /// Callback used by the visibility system to determine if an observer (player) can see this object.
        /// <para>If this function returns true, the network connection will be added as an observer.</para>
        /// </summary>
        /// <param name="conn">Network connection of a player.</param>
        /// <returns>True if the player can see this object.</returns>
        public override bool OnCheckObserver(NetworkConnection conn)
        {
            // Not Visible if not in a match
            if (matchId == Guid.Empty)
                return false;

            NetworkMatchChecker networkMatchChecker = conn.identity.GetComponent<NetworkMatchChecker>();

            if (networkMatchChecker == null)
                return false;

            return networkMatchChecker.matchId == matchId;
        }

        /// <summary>
        /// Callback used by the visibility system to (re)construct the set of observers that can see this object.
        /// <para>Implementations of this callback should add network connections of players that can see this object to the observers set.</para>
        /// </summary>
        /// <param name="observers">The new set of observers for this object.</param>
        /// <param name="initialize">True if the set of observers is being built for the first time.</param>
        public override void OnRebuildObservers(HashSet<NetworkConnection> observers, bool initialize)
        {
            if (currentMatch == Guid.Empty) return;

            foreach (NetworkIdentity networkIdentity in matchPlayers[currentMatch])
                if (networkIdentity != null && networkIdentity.connectionToClient != null)
                    observers.Add(networkIdentity.connectionToClient);
        }

        #endregion
    }
}
