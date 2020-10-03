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

                // check distance with each player connection
                // TODO check with each player connection's owned entities
                // (a monster is visible to a player, if either the player or
                //  the player's pet sees it)
                foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
                {
                    float distance = Vector3.Distance(identity.transform.position, conn.identity.transform.position);
                    if (distance <= visibilityRadius)
                    {
                        // add to rebuild
                        identity.rebuild.Add(conn);
                    }
                }

                //if (identity.rebuild.Count > 0)
                //    Debug.Log($"{identity.name} is observed by {identity.rebuild.Count} connections");
            }
        }

        // remove old observers and send unspawn messages
        void RemoveOldObservers()
        {
            // foreach spawned
            foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values)
            {
                // foreach observer
                // TODO rename observars after removing the old code
                for (int i = 0; i < identity.observars.Count; ++i)
                {
                    //Debug.Log($"{identity.name} had {identity.observars.Count} observers and rebuild has {identity.rebuild.Count}");

                    // not in rebuild? then we need to remove and unspawn
                    NetworkConnectionToClient conn = identity.observars[i];
                    if (!identity.rebuild.Contains(conn))
                    {
                        // TODO send unspawn message
                        Debug.LogWarning($"TODO Unspawn {identity.name} for connectionId {conn.connectionId}");

                        // remove it from observers
                        identity.observars.RemoveAt(i);
                        --i;
                    }
                }
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
                    // TODO use set to avoid O(n) contains
                    if (!identity.observars.Contains(conn))
                    {
                        // is the entity owned by the observer connection?
                        bool owned = identity.connectionToClient != null &&
                                     identity.connectionToClient.connectionId == conn.connectionId;

                        // TODO send spawn message with owned flag
                        Debug.LogWarning($"TODO Spawn {identity.name} for connectionId {conn.connectionId}");

                        // add it to observers
                        identity.observars.Add(conn);
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
