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
        public const string Message = "Per-NetworkIdentity Interest Management is being replaced by global Interest Management.\n\nWe already converted some components to the new system. For those, please remove Proximity checkers from NetworkIdentity prefabs and add one global InterestManagement component to your NetworkManager instead. If we didn't convert this one yet, then simply wait. See our Benchmark example and our Mirror/Components/InterestManagement for available implementations.\n\nIf you need to port a custom solution, move your code into a new class that inherits from InterestManagement and add one global update method instead of using NetworkBehaviour.Update.\n\nDon't panic. The whole change mostly moved code from NetworkVisibility components into one global place on NetworkManager. Allows for Spatial Hashing which is ~30x faster.\n\n(╯°□°)╯︵ ┻━┻";
    }

    // Deprecated 2021-02-17
    [Obsolete(NetworkVisibilityObsoleteMessage.Message)]
    [DisallowMultipleComponent]
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
