// straight forward Vector3.Distance based interest management.
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public class DistanceInterestManagement : InterestManagement
    {
        [Tooltip("The maximum range that objects will be visible at.")]
        public int visRange = 10;

        [Tooltip("Rebuild all every 'rebuildInterval' seconds.")]
        public float rebuildInterval = 1;
        double lastRebuildTime;

        private Dictionary<NetworkIdentity, DistanceVisRangeOverride> rangeOverrides =
            new Dictionary<NetworkIdentity, DistanceVisRangeOverride>();

        public override void OnSpawned(NetworkIdentity identity)
        {
            DistanceVisRangeOverride visOverride = identity.GetComponent<DistanceVisRangeOverride>();
            if (visOverride)
            {
                rangeOverrides[identity] = visOverride;
            }
        }

        public override void OnDestroyed(NetworkIdentity identity)
        {
            rangeOverrides.Remove(identity);
        }

        public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnection newObserver)
        {
            float range = visRange;
            if (rangeOverrides.TryGetValue(identity, out DistanceVisRangeOverride rangeOverride))
            {
                range = rangeOverride.visRange;
            }
            return Vector3.Distance(identity.transform.position, newObserver.identity.transform.position) <= range;
        }

        public override void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnection> newObservers, bool initialize)
        {
            // 'transform.' calls GetComponent, only do it once
            Vector3 position = identity.transform.position;

            float range = visRange;
            if (rangeOverrides.TryGetValue(identity, out DistanceVisRangeOverride rangeOverride))
            {
                range = rangeOverride.visRange;
            }

            // brute force distance check
            // -> only player connections can be observers, so it's enough if we
            //    go through all connections instead of all spawned identities.
            // -> compared to UNET's sphere cast checking, this one is orders of
            //    magnitude faster. if we have 10k monsters and run a sphere
            //    cast 10k times, we will see a noticeable lag even with physics
            //    layers. but checking to every connection is fast.
            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            {
                // authenticated and joined world with a player?
                if (conn != null && conn.isAuthenticated && conn.identity != null)
                {

                    // check distance
                    if (Vector3.Distance(conn.identity.transform.position, position) < range)
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

            // rebuild all spawned NetworkIdentity's observers every interval
            if (NetworkTime.localTime >= lastRebuildTime + rebuildInterval)
            {
                RebuildAll();
                lastRebuildTime = NetworkTime.localTime;
            }
        }
    }
}
