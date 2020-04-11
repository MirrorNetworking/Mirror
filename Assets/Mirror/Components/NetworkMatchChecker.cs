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

        public Guid matchId
        {
            get { return currentMatch; }
            set
            {
                if (currentMatch == value) return;

                if (currentMatch != Guid.Empty)
                {
                    // Remove this object from the hashset of the match it just left
                    matchPlayers[currentMatch].Remove(netIdentity);

                    // RebuildObservers of all NetworkIdentity's in the match this object just left
                    RebuildMatchObservers();
                }

                // Set this to the new match this object just entered
                currentMatch = value;
                // ... and copy the string for the inspector because Unity can't show Guid directly
                currentMatchDebug = currentMatch.ToString();

                if (currentMatch != Guid.Empty)
                {
                    // Make sure this new match is in the dictionary
                    if (!matchPlayers.ContainsKey(currentMatch))
                        matchPlayers.Add(currentMatch, new HashSet<NetworkIdentity>());

                    // Add this object to the hashset of the new match
                    matchPlayers[currentMatch].Add(netIdentity);

                    // RebuildObservers of all NetworkIdentity's in the match this object just entered
                    RebuildMatchObservers();
                }
            }
        }

        public override void OnStartServer()
        {
            if (currentMatch == Guid.Empty) return;

            if (!matchPlayers.ContainsKey(currentMatch))
                matchPlayers.Add(currentMatch, new HashSet<NetworkIdentity>());

            matchPlayers[currentMatch].Add(netIdentity);
        }

        void RebuildMatchObservers()
        {
            if (currentMatch == Guid.Empty) return;

            foreach (NetworkIdentity networkIdentity in matchPlayers[currentMatch])
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
            return conn.identity.GetComponent<NetworkMatchChecker>().matchId == matchId;
        }

        /// <summary>
        /// Callback used by the visibility system to (re)construct the set of observers that can see this object.
        /// <para>Implementations of this callback should add network connections of players that can see this object to the observers set.</para>
        /// </summary>
        /// <param name="observers">The new set of observers for this object.</param>
        /// <param name="initialize">True if the set of observers is being built for the first time.</param>
        /// <returns>true when overwriting so that Mirror knows that we wanted to rebuild observers ourselves. otherwise it uses built in rebuild.</returns>
        public override void OnRebuildObservers(HashSet<NetworkConnection> observers, bool initialize)
        {
            if (currentMatch == Guid.Empty) return;

            foreach (NetworkIdentity networkIdentity in matchPlayers[currentMatch])
                if (networkIdentity != null && networkIdentity.connectionToClient != null)
                    observers.Add(networkIdentity.connectionToClient);
        }

        /// <summary>
        /// Callback used by the visibility system for objects on a host. This is only called on local clients on a host.
        /// <para>Objects on a host (with a local client) cannot be disabled or destroyed when they are not visibile to the local client.
        /// <para>This function is called to allow custom code to hide these objects. A typical implementation will disable renderer components on the object.</para>
        /// </summary>
        /// <param name="visible">New visibility state.</param>
        public override void OnSetHostVisibility(bool visible)
        {
            foreach (Renderer rend in GetComponentsInChildren<Renderer>())
                rend.enabled = visible;
        }

        #endregion
    }
}
