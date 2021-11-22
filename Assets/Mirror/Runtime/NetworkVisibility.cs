using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    // the name NetworkProximityCheck implies that it's only about objects in
    // proximity to the player. But we might have room based, guild based,
    // instanced based checks too, so NetworkVisibility is more fitting.
    //
    // note: we inherit from NetworkBehaviour so we can reuse .netIdentity, etc.
    // note: unlike UNET, we only allow 1 proximity checker per NetworkIdentity.

    // Deprecated 2021-10-30
    [DisallowMultipleComponent]
    [Obsolete("Network Visibility has been deprecated. Use Global Interest Management instead. Click ? button on this component for details.")]
    [HelpURL("https://mirror-networking.gitbook.io/docs/guides/interest-management")]
    public abstract class NetworkVisibility : NetworkBehaviour
    {
        /// <summary>Callback used by the visibility system to determine if an observer (player) can see this object.</summary>
        // Called from NetworkServer.SpawnObserversForConnection the first time
        // a NetworkIdentity is spawned.
        public abstract bool OnCheckObserver(NetworkConnection conn);

        /// <summary>Callback used by the visibility system to (re)construct the set of observers that can see this object.</summary>
        // Implementations of this callback should add network connections of
        // players that can see this object to the observers set.
        public abstract void OnRebuildObservers(HashSet<NetworkConnection> observers, bool initialize);

        /// <summary>Callback used by the visibility system for objects on a host.</summary>
        public virtual void OnSetHostVisibility(bool visible)
        {
            foreach (Renderer rend in GetComponentsInChildren<Renderer>())
                rend.enabled = visible;
        }
    }
}
