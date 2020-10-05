// straight forward brute force interest management from DOTSNET
using UnityEngine;

namespace Mirror
{
    public class BruteForceInterestManagement : InterestManagement
    {
        // visibility radius
        public float visibilityRadius = float.MaxValue;

        // don't update every tick. update every so often.
        public float updateInterval = 1;
        double lastUpdateTime;

        // rebuild observers and store the result in rebuild buffer
        void RebuildObservers()
        {
            // for each NetworkIdentity, we need to check if it's visible from
            // ANY of the player's entities. not just the main player.
            //
            // consider a MOBA game where a player might place a watchtower at
            // the other end of the map:
            // * if we check visibility only to the main player, then the watch-
            //   tower would not see anything
            // * if we check visibility to all player objects, both the watch-
            //   tower and the main player object would see enemies

            // foreach spawned
            foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values)
            {
                // clear previous rebuild first
                identity.rebuild.Clear();

                // only add observers if not currently hidden from observers
                if (!identity.forceHidden)
                {
                    // check distance with each player connection
                    // TODO check with each player connection's owned entities
                    // (a monster is visible to a player, if either the player or
                    //  the player's pet sees it)
                    foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
                    {
                        // only if joined the world (=ready) and selected a player yet
                        if (conn.isReady && conn.identity != null)
                        {
                            float distance = Vector3.Distance(identity.transform.position, conn.identity.transform.position);
                            if (distance <= visibilityRadius)
                            {
                                // add to rebuild
                                identity.rebuild.Add(conn);
                            }
                        }
                    }
                }

                //if (identity.rebuild.Count > 0)
                //    Debug.Log($"{identity.name} is observed by {identity.rebuild.Count} connections");
            }
        }

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

        public override void RebuildAll()
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
