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

    // obsolete message in a not-obsolete class to avoid obsolete warnings when
    // obsoleting with the obsolete message. got it?
    public static class NetworkVisibilityObsoleteMessage
    {
        // obsolete message in one place. show them everywhere.
        public const string Message = "Per-NetworkIdentity Interest Management is being replaced by global Interest Management.\n\nPlease remove Proximity checkers from NetworkIdentity prefabs and add one global InterestManagement component to your NetworkManager instead. See our Benchmark example and our Mirror/Components/InterestManagement for available implementations.\n\nIf you need to port a custom solution, move your code into a new class that inherits from InterestManagement and add one global update method instead of using NetworkBehaviour.Update.\n\nDon't panic. The whole change mostly moved code from NetworkVisibility components into one global place on NetworkManager. Allows for Spatial Hashing which is ~30x faster.\n\n(╯°□°)╯︵ ┻━┻";
    }

    [Obsolete(NetworkVisibilityObsoleteMessage.Message)]
    [DisallowMultipleComponent]
    public abstract class NetworkVisibility : NetworkBehaviour
    {
        /// <summary>
        /// Callback used by the visibility system to determine if an observer (player) can see this object.
        /// <para>If this function returns true, the network connection will be added as an observer.</para>
        /// </summary>
        /// <param name="conn">Network connection of a player.</param>
        /// <returns>True if the player can see this object.</returns>
        public abstract bool OnCheckObserver(NetworkConnection conn);

        /// <summary>
        /// Callback used by the visibility system to (re)construct the set of observers that can see this object.
        /// <para>Implementations of this callback should add network connections of players that can see this object to the observers set.</para>
        /// </summary>
        /// <param name="observers">The new set of observers for this object.</param>
        /// <param name="initialize">True if the set of observers is being built for the first time.</param>
        public abstract void OnRebuildObservers(HashSet<NetworkConnection> observers, bool initialize);

        /// <summary>
        /// Callback used by the visibility system for objects on a host.
        /// <para>Objects on a host (with a local client) cannot be disabled or destroyed when they are not visible to the local client. So this function is called to allow custom code to hide these objects. A typical implementation will disable renderer components on the object. This is only called on local clients on a host.</para>
        /// </summary>
        /// <param name="visible">New visibility state.</param>
        public virtual void OnSetHostVisibility(bool visible)
        {
            foreach (Renderer rend in GetComponentsInChildren<Renderer>())
                rend.enabled = visible;
        }
    }
}
