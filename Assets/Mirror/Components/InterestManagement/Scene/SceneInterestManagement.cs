// straight forward Vector3.Distance based interest management.
using System.Collections.Generic;

namespace Mirror
{
    public class SceneInterestManagement : InterestManagement
    {
        public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnection newObserver)
        {
            // the newly spawned 'newObserver' can see the already existing
            // 'identity' if they both have the same scene.
            return newObserver.identity.gameObject.scene == identity.gameObject.scene;
        }

        // for this 'identity', put everyone who sees it into 'newObservers'
        public override void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnection> newObservers, bool initialize)
        {
            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            {
                // authenticated and joined world with a player?
                if (conn != null && conn.isAuthenticated && conn.identity != null)
                {
                    // check scene
                    if (identity.gameObject.scene == conn.identity.gameObject.scene)
                    {
                        newObservers.Add(conn);
                    }
                }
            }
        }

        void Update()
        {
            // only on server
            if (!NetworkServer.active) return;

            // rebuild all spawned NetworkIdentity's observers every update for
            // now. later on, only on scene changes?
            RebuildAll();
        }
    }
}
