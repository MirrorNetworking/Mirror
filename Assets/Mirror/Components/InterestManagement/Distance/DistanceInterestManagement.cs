// straight forward Vector3.Distance based interest management.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/ Interest Management/ Distance/Distance Interest Management")]
    public class DistanceInterestManagement : InterestManagement
    {
        [Tooltip("The maximum range that objects will be visible at. Add DistanceInterestManagementCustomRange onto NetworkIdentities for custom ranges.")]
        public int visRange = 500;

        [Tooltip("Minimum distance an object has to move before we rebuild observers for it. This is a performance optimization to avoid rebuilding observers every frame for objects that are moving but haven't moved far enough to change their visibility.")]
        [Range(0.1f, 100f)]
        public float minMoveDistance = 0.1f;

        [Tooltip("Rebuild all in seconds.")]
        [Range(1, 60)]
        public float rebuildInterval = 1;
        double lastRebuildTime;

        [Tooltip("Rebuild static objects every nth rebuild interval.\nThis is a performance optimization to avoid rebuilding observers every regular interval for static objects that haven't moved.")]
        [Range(1, 60)]
        public byte staticRebuildInterval = 10;
        byte rebuildCounter = 0;

        // cache custom ranges to avoid runtime TryGetComponent lookups
        readonly Dictionary<NetworkIdentity, DistanceInterestManagementCustomRange> CustomRanges = new Dictionary<NetworkIdentity, DistanceInterestManagementCustomRange>();

        // cache connections and their positions to avoid runtime .transform lookups in OnRebuildObservers.
        readonly List<(NetworkConnectionToClient conn, Vector3 pos)> cachedConnections = new List<(NetworkConnectionToClient, Vector3)>();

        // cache identity positions to avoid runtime .transform lookups in LateUpdate.
        readonly Dictionary<NetworkIdentity, Vector3> lastIdentityPositions = new Dictionary<NetworkIdentity, Vector3>();

        // cache dirty identities to avoid rebuilding observers every frame for all identities. only rebuild those that moved.
        readonly HashSet<NetworkIdentity> dirtyIdentities = new HashSet<NetworkIdentity>();

        // cache static objects to avoid rebuilding observers every frame for static objects that haven't moved.
        readonly HashSet<NetworkIdentity> staticObjects = new HashSet<NetworkIdentity>();

        // helper function to get vis range for a given object, or default.
        [ServerCallback]
        int GetVisRange(NetworkIdentity identity)
        {
            return CustomRanges.TryGetValue(identity, out DistanceInterestManagementCustomRange custom) ? custom.visRange : visRange;
        }

        [ServerCallback]
        public override void ResetState()
        {
            lastRebuildTime = 0D;
            CustomRanges.Clear();
            cachedConnections.Clear();
            staticObjects.Clear();
            lastIdentityPositions.Clear();
            dirtyIdentities.Clear();
            rebuildCounter = 0;
        }

        [ServerCallback]
        public override void OnSpawned(NetworkIdentity identity)
        {
            if (identity.TryGetComponent(out DistanceInterestManagementCustomRange custom))
                CustomRanges[identity] = custom;

            Renderer[] renderers = identity.gameObject.GetComponentsInChildren<Renderer>();
            if (renderers.Any(r => r.isPartOfStaticBatch))
                staticObjects.Add(identity);

            dirtyIdentities.Add(identity); // always rebuild once on spawn
        }

        [ServerCallback]
        public override void OnDestroyed(NetworkIdentity identity)
        {
            CustomRanges.Remove(identity);
            lastIdentityPositions.Remove(identity);
            dirtyIdentities.Remove(identity);
            staticObjects.Remove(identity);            // Ensure it's removed from static set if present
        }

        [ServerCallback]
        public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnectionToClient newObserver)
        {
            if (newObserver == null || newObserver.identity == null)
                return false;

            int range = GetVisRange(identity);
            return (identity.transform.position - newObserver.identity.transform.position).sqrMagnitude < (long)range * range;
        }

        [ServerCallback]
        public override void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnectionToClient> newObservers)
        {
            int range = GetVisRange(identity);
            long rangeSq = (long)range * range;
            Vector3 position = identity.transform.position;

            if (cachedConnections.Count > 0)
            {
                foreach ((NetworkConnectionToClient conn, Vector3 connPos) in cachedConnections)
                    if ((connPos - position).sqrMagnitude < rangeSq)
                        newObservers.Add(conn);
            }
            else
            {
                foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
                    if (conn != null && conn.isAuthenticated && conn.identity != null &&
                        (conn.identity.transform.position - position).sqrMagnitude < rangeSq)
                        newObservers.Add(conn);
            }
        }

        [ServerCallback]
        void LateUpdate()
        {
            if (NetworkTime.localTime >= lastRebuildTime + rebuildInterval)
            {
                RebuildConnectionCache();

                // exact cadence: static rebuild every N intervals
                rebuildCounter++;
                bool updateStatic = rebuildCounter >= staticRebuildInterval;
                float minMoveDistanceSq = minMoveDistance * minMoveDistance;

                // Detect player movement BEFORE updating lastIdentityPositions in the spawned loop.
                bool anyPlayerMoved = false;
                foreach ((NetworkConnectionToClient conn, Vector3 pos) in cachedConnections)
                {
                    if (!lastIdentityPositions.TryGetValue(conn.identity, out Vector3 last) ||
                        (pos - last).sqrMagnitude >= minMoveDistanceSq)
                    {
                        anyPlayerMoved = true;
                        break;
                    }
                }

                if (anyPlayerMoved)
                {
                    foreach (NetworkIdentity id in NetworkServer.spawned.Values)
                        dirtyIdentities.Add(id);
                }

                // Mark identities that moved as dirty; skip static unless static interval is due
                foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
                {
                    if (!updateStatic && staticObjects.Contains(identity))
                        continue;

                    Vector3 pos = identity.transform.position;
                    if (!lastIdentityPositions.TryGetValue(identity, out Vector3 last) ||
                        (pos - last).sqrMagnitude >= minMoveDistanceSq)
                    {
                        lastIdentityPositions[identity] = pos;
                        dirtyIdentities.Add(identity);
                    }
                }

                foreach (NetworkIdentity identity in dirtyIdentities)
                    NetworkServer.RebuildObservers(identity, false);

                dirtyIdentities.Clear();

                if (updateStatic)
                    rebuildCounter = 0;

                lastRebuildTime = NetworkTime.localTime;
            }
        }

        [ServerCallback]
        void RebuildConnectionCache()
        {
            cachedConnections.Clear();
            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
                if (conn != null && conn.isAuthenticated && conn.identity != null)
                    cachedConnections.Add((conn, conn.identity.transform.position));
        }
    }
}
