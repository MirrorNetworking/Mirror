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
    // Deprecated 2021-07-16
    [Obsolete(NetworkVisibilityObsoleteMessage.Message)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkMatchChecker")]
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(NetworkMatch))]
    [HelpURL("https://mirror-networking.gitbook.io/docs/components/network-match-checker")]
    public class NetworkMatchChecker : NetworkVisibility
    {
        // internal for tests
        internal static readonly Dictionary<Guid, HashSet<NetworkIdentity>> matchPlayers =
            new Dictionary<Guid, HashSet<NetworkIdentity>>();

        // internal for tests
        internal Guid currentMatch
        {
            get => GetComponent<NetworkMatch>().matchId;
            set => GetComponent<NetworkMatch>().matchId = value;
        }

        internal Guid lastMatch;

        public override void OnStartServer()
        {
            if (currentMatch == Guid.Empty) return;

            if (!matchPlayers.ContainsKey(currentMatch))
                matchPlayers.Add(currentMatch, new HashSet<NetworkIdentity>());

            matchPlayers[currentMatch].Add(netIdentity);

            // No need to rebuild anything here.
            // identity.RebuildObservers is called right after this from NetworkServer.SpawnObject
        }

        public override void OnStopServer()
        {
            if (currentMatch == Guid.Empty) return;

            if (matchPlayers.ContainsKey(currentMatch) && matchPlayers[currentMatch].Remove(netIdentity))
                RebuildMatchObservers(currentMatch);
        }

        void RebuildMatchObservers(Guid specificMatch)
        {
            foreach (NetworkIdentity networkIdentity in matchPlayers[specificMatch])
                networkIdentity?.RebuildObservers(false);
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
            if (currentMatch == Guid.Empty)
                return false;

            NetworkMatchChecker networkMatchChecker = conn.identity.GetComponent<NetworkMatchChecker>();

            if (networkMatchChecker == null)
                return false;

            return networkMatchChecker.currentMatch == currentMatch;
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

        [ServerCallback]
        void Update()
        {
            // only if changed
            if (currentMatch == lastMatch)
                return;

            // This object is in a new match so observers in the prior match
            // and the new match need to rebuild their respective observers lists.

            // Remove this object from the hashset of the match it just left
            if (lastMatch != Guid.Empty)
            {
                matchPlayers[lastMatch].Remove(netIdentity);

                // RebuildObservers of all NetworkIdentity's in the match this
                // object just left
                RebuildMatchObservers(lastMatch);
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

            // save last rebuild's match
            lastMatch = currentMatch;
        }
    }
}
