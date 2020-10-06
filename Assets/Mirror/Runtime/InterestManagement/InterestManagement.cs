// interest management from DOTSNET
using UnityEngine;

namespace Mirror
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkManager))]
    public abstract class InterestManagement : MonoBehaviour
    {
        // don't update every tick. update every so often.
        public float updateInterval = 1;
        double lastUpdateTime;

        // configure NetworkServer
        protected virtual void Awake() { NetworkServer.interestManagement = this; }

        // rebuild observers and store the results in each spawned
        // NetworkIdentity's rebuild buffer
        protected abstract void RebuildObservers();

        void RemoveOldObservers()
        {
            // foreach spawned
            foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values)
            {
                // unspawn all observers that are NOT in rebuild anymore
                foreach (NetworkConnectionToClient observer in identity.observers)
                {
                    //Debug.Log($"{identity.name} had {identity.observars.Count} observers and rebuild has {identity.rebuild.Count}");
                    if (!identity.rebuild.Contains(observer))
                    {
                        // unspawn identity for this connection
                        //Debug.LogWarning($"Unspawning {identity.name} for connectionId {conn.connectionId}");
                        NetworkServer.HideForConnection(identity, observer);
                    }
                }

                // we can't iterate and remove from HashSet at the same time.
                // so remove all observers that are NOT in rebuild now.
                // (in other words, keep observers that are in both hashsets)
                //
                // note: IntersectWith version that returns removed values would
                //       be even faster.
                identity.observers.IntersectWith(identity.rebuild);
            }
        }

        // add new observers and send spawn messages
        void AddNewObservers()
        {
            // foreach spawned
            foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values)
            {
                // foreach rebuild
                foreach (NetworkConnectionToClient conn in identity.rebuild)
                {
                    // was it not in old observers?
                    if (!identity.observers.Contains(conn))
                    {
                        // spawn identity for this connection
                        //Debug.LogWarning($"Spawning {identity.name} for connectionId {conn.connectionId}");
                        NetworkServer.ShowForConnection(identity, conn);

                        // add it to observers
                        identity.observers.Add(conn);
                    }
                }
            }
        }

        // rebuild all areas of interest for everyone once.
        // the three rebuild steps are always the same. only RebuildObservers is
        // different depending on the solution.
        //
        // note:
        //   we DO NOT do any custom rebuilding after someone joined/spawned or
        //   disconnected/unspawned.
        //   this would require INSANE complexity.
        //   for example, OnTransportDisconnect would have to:
        //     1. remove the connection so the connectionId is invalid
        //     2. then call BroadcastAfterUnspawn(oldEntity) which broadcasts
        //        destroyed messages BEFORE rebuilding so we know the old
        //        observers that need to get the destroyed message
        //     3. RebuildAfterUnspawn to remove it
        //     4. then remove the Entity from connection's owned objects, which
        //        IS NOT POSSIBLE anymore because the connection was already
        //        removed. which means that the next rebuild would still see it
        //        etc.
        //        (it's just insanity)
        //   additionally, we would also need extra flags in Spawn to NOT
        //   rebuild when spawning 10k scene objects in start, etc.s
        //
        // first principles:
        //   it wouldn't even make sense to have special cases because players
        //   might walk in and out of range from each other all the time anyway.
        //   we already need to handle that case. (dis)connect is no different.
        //
        public void RebuildAll()
        {
            RebuildObservers();
            RemoveOldObservers();
            AddNewObservers();
        }

        // update rebuilds every couple of seconds
        void Update()
        {
            // only while server is running
            if (NetworkServer.active)
            {
                if (Time.time >= lastUpdateTime + updateInterval)
                {
                    RebuildAll();
                    lastUpdateTime = Time.time;
                }
            }
        }
    }
}
