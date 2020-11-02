using UnityEngine;
using System.Collections.Generic;

namespace Mirror
{
    /// <summary>
    /// Component that limits visibility of networked objects to the authority client.
    /// <para>Any object with this component on it will only be visible to the client that has been assigned authority for it.</para>
    /// <para>This would be used for spawning a non-player networked object for single client to interact with, e.g. in-game puzzles.</para>
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkOwnerChecker")]
    [RequireComponent(typeof(NetworkIdentity))]
    [HelpURL("https://mirror-networking.com/docs/Components/NetworkOwnerChecker.html")]
    public class NetworkOwnerChecker : NetworkVisibility
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(NetworkSceneChecker));

        /// <summary>
        /// Callback used by the visibility system to determine if an observer (player) can see this object.
        /// <para>If this function returns true, the network connection will be added as an observer.</para>
        /// </summary>
        /// <param name="conn">Network connection of a player.</param>
        /// <returns>True if the client is the owner of this object.</returns>
        public override bool OnCheckObserver(NetworkConnection conn)
        {
            if (logger.LogEnabled()) logger.Log($"OnCheckObserver {netIdentity.connectionToClient} {conn}");

            return (netIdentity.connectionToClient == conn);
        }

        /// <summary>
        /// Callback used by the visibility system to (re)construct the set of observers that can see this object.
        /// </summary>
        /// <param name="observers">The new set of observers for this object.</param>
        /// <param name="initialize">True if the set of observers is being built for the first time.</param>
        public override void OnRebuildObservers(HashSet<NetworkConnection> observers, bool initialize)
        {
            if (logger.LogEnabled()) logger.Log($"OnRebuildObservers {gameObject} {netIdentity.connectionToClient}");

            // early out for non-player objects with no owner
            if (netIdentity.connectionToClient == null)
                return;

            // iterate connections.Values looking for the owner
            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            {
                if (conn != null && conn.identity != null && netIdentity.connectionToClient == conn)
                {
                    // found a match - add it to the list
                    observers.Add(conn);

                    // no need to continue since there can be only one owner
                    break;
                }
            }
        }
    }
}
