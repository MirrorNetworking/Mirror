using System;
using UnityEngine;
using System.Collections.Generic;

namespace Mirror
{
    /// <summary>
    /// Component that limits visibility of networked objects to the authority client.
    /// <para>Any object with this component on it will only be visible to the client that has been assigned authority for it.</para>
    /// <para>This would be used for spawning a non-player networked object for single client to interact with, e.g. in-game puzzles.</para>
    /// </summary>
    [Obsolete(NetworkVisibilityObsoleteMessage.Message)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkOwnerChecker")]
    [RequireComponent(typeof(NetworkIdentity))]
    [HelpURL("https://mirror-networking.gitbook.io/docs/components/network-owner-checker")]
    public class NetworkOwnerChecker : NetworkVisibility
    {
        /// <summary>
        /// Callback used by the visibility system to determine if an observer (player) can see this object.
        /// <para>If this function returns true, the network connection will be added as an observer.</para>
        /// </summary>
        /// <param name="conn">Network connection of a player.</param>
        /// <returns>True if the client is the owner of this object.</returns>
        public override bool OnCheckObserver(NetworkConnection conn)
        {
            // Debug.Log($"OnCheckObserver {netIdentity.connectionToClient} {conn}");

            return (netIdentity.connectionToClient == conn);
        }

        /// <summary>
        /// Callback used by the visibility system to (re)construct the set of observers that can see this object.
        /// </summary>
        /// <param name="observers">The new set of observers for this object.</param>
        /// <param name="initialize">True if the set of observers is being built for the first time.</param>
        public override void OnRebuildObservers(HashSet<NetworkConnection> observers, bool initialize)
        {
            // Do nothing here because the authority client is always added as an observer internally.
        }
    }
}
