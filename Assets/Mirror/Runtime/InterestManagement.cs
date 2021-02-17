// interest management component for custom solutions like
// distance based, spatial hashing, raycast based, etc.
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    [DisallowMultipleComponent]
    public abstract class InterestManagement : MonoBehaviour
    {
        // Awake configures InterestManagement in NetworkServer
        void Awake()
        {
            if (NetworkServer.aoi == null)
            {
                NetworkServer.aoi = this;
            }
            else Debug.LogError($"Only one InterestManagement component allowed. {NetworkServer.aoi.GetType()} has been set up already.");
        }

        // Callback used by the visibility system to determine if an observer
        // (player) can see the NetworkIdentity. If this function returns true,
        // the network connection will be added as an observer.
        //   conn: Network connection of a player.
        //   returns True if the player can see this object.
        public abstract bool OnCheckObserver(NetworkIdentity identity, NetworkConnection newObserver);

        // rebuild observers for the given NetworkIdentity.
        // Server will automatically spawn/despawn added/removed ones.
        //   newObservers: cached hashset to put the result into
        //   initialize: true if being rebuilt for the first time
        //
        // IMPORTANT:
        // => global rebuild would be more simple, BUT
        // => local rebuild is way faster for spawn/despawn because we can
        //    simply rebuild a select NetworkIdentity only
        // => having both .observers and .observing is necessary for local
        //    rebuilds
        //
        // in other words, this is the perfect solution even though it's not
        // completely simple (due to .observers & .observing).
        //
        // Mirror maintains .observing automatically in the background. best of
        // both worlds without any worrying now!
        public abstract void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnection> newObservers, bool initialize);

        // helper function to trigger a full rebuild.
        // most implementations should call this in a certain interval.
        // some might call this all the time, or only on team changes or
        // scene changes and so on.
        //
        // IMPORTANT: check if NetworkServer.active when using Update()!
        protected void RebuildAll()
        {
            foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values)
            {
                NetworkServer.RebuildObservers(identity, false);
            }
        }
    }
}
