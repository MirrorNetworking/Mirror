// straight forward brute force interest management from DOTSNET
using UnityEngine;

namespace Mirror
{
    public class BruteForceInterestManagement : InterestManagement
    {
        // visibility radius
        public float visibilityRadius = float.MaxValue;

        // rebuild observers and store the result in rebuild buffer
        protected override void RebuildObservers()
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
                // clear previous rebuild in any case
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
    }
}
