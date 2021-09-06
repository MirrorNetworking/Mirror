using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// Component that controls visibility of networked objects for players.
    /// <para>Any object with this component on it will not be visible to players more than a (configurable) distance away.</para>
    /// </summary>
    // Deprecated 2021-07-13
    [Obsolete(NetworkVisibilityObsoleteMessage.Message)]
    [AddComponentMenu("Network/NetworkProximityChecker")]
    [RequireComponent(typeof(NetworkIdentity))]
    [HelpURL("https://mirror-networking.gitbook.io/docs/components/network-proximity-checker")]
    public class NetworkProximityChecker : NetworkVisibility
    {
        /// <summary>
        /// The maximum range that objects will be visible at.
        /// </summary>
        [Tooltip("The maximum range that objects will be visible at.")]
        public int visRange = 10;

        /// <summary>
        /// How often (in seconds) that this object should update the list of observers that can see it.
        /// </summary>
        [Tooltip("How often (in seconds) that this object should update the list of observers that can see it.")]
        public float visUpdateInterval = 1;

        public override void OnStartServer()
        {
            InvokeRepeating(nameof(RebuildObservers), 0, visUpdateInterval);
        }
        public override void OnStopServer()
        {
            CancelInvoke(nameof(RebuildObservers));
        }

        void RebuildObservers()
        {
            netIdentity.RebuildObservers(false);
        }

        /// <summary>
        /// Callback used by the visibility system to determine if an observer (player) can see this object.
        /// <para>If this function returns true, the network connection will be added as an observer.</para>
        /// </summary>
        /// <param name="conn">Network connection of a player.</param>
        /// <returns>True if the player can see this object.</returns>
        public override bool OnCheckObserver(NetworkConnection conn)
        {
            return Vector3.Distance(conn.identity.transform.position, transform.position) < visRange;
        }

        /// <summary>
        /// Callback used by the visibility system to (re)construct the set of observers that can see this object.
        /// <para>Implementations of this callback should add network connections of players that can see this object to the observers set.</para>
        /// </summary>
        /// <param name="observers">The new set of observers for this object.</param>
        /// <param name="initialize">True if the set of observers is being built for the first time.</param>
        public override void OnRebuildObservers(HashSet<NetworkConnection> observers, bool initialize)
        {
            // if force hidden then return without adding any observers.
            // 'transform.' calls GetComponent, only do it once
            Vector3 position = transform.position;

            // brute force distance check
            // -> only player connections can be observers, so it's enough if we
            //    go through all connections instead of all spawned identities.
            // -> compared to UNET's sphere cast checking, this one is orders of
            //    magnitude faster. if we have 10k monsters and run a sphere
            //    cast 10k times, we will see a noticeable lag even with physics
            //    layers. but checking to every connection is fast.
            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            {
                if (conn != null && conn.identity != null)
                {
                    // check distance
                    if (Vector3.Distance(conn.identity.transform.position, position) < visRange)
                    {
                        observers.Add(conn);
                    }
                }
            }
        }
    }
}
