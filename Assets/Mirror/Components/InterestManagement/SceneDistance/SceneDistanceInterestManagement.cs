using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror
{
    [AddComponentMenu("Network/ Interest Management/ Scene/Scene Distance Interest Management")]
    public class SceneDistanceInterestManagement : InterestManagement
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

        // Use Scene instead of string scene.name because when additively
        // loading multiples of a subscene the name won't be unique
        readonly Dictionary<Scene, HashSet<NetworkIdentity>> sceneObjects =
            new Dictionary<Scene, HashSet<NetworkIdentity>>();

        readonly Dictionary<NetworkIdentity, Scene> lastObjectScene =
            new Dictionary<NetworkIdentity, Scene>();

        HashSet<Scene> dirtyScenes = new HashSet<Scene>();

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
            sceneObjects.Clear();
            lastObjectScene.Clear();
            dirtyScenes.Clear();
            rebuildCounter = 0;
        }

        [ServerCallback]
        public override void OnSpawned(NetworkIdentity identity)
        {
            if (identity.TryGetComponent(out DistanceInterestManagementCustomRange custom))
                CustomRanges[identity] = custom;

            if (identity.GetComponentsInChildren<Renderer>().Any(r => r.isPartOfStaticBatch))
                staticObjects.Add(identity);

            Scene currentScene = identity.gameObject.scene;
            lastObjectScene[identity] = currentScene;
            lastIdentityPositions[identity] = identity.transform.position;
            dirtyIdentities.Add(identity); // always rebuild once on spawn

            // Debug.Log($"SceneInterestManagement.OnSpawned({identity.name}) currentScene: {currentScene}");
            if (!sceneObjects.TryGetValue(currentScene, out HashSet<NetworkIdentity> objects))
            {
                objects = new HashSet<NetworkIdentity>();
                sceneObjects.Add(currentScene, objects);
            }

            objects.Add(identity);
        }

        [ServerCallback]
        public override void OnDestroyed(NetworkIdentity identity)
        {
            CustomRanges.Remove(identity);
            lastIdentityPositions.Remove(identity);
            dirtyIdentities.Remove(identity);
            staticObjects.Remove(identity);

            // Don't RebuildSceneObservers here - that will happen in LateUpdate.
            // Multiple objects could be destroyed in same frame and we don't
            // want to rebuild for each one...let LateUpdate do it once.
            // We must add the current scene to dirtyScenes for LateUpdate to rebuild it.
            if (lastObjectScene.TryGetValue(identity, out Scene currentScene))
            {
                lastObjectScene.Remove(identity);
                if (sceneObjects.TryGetValue(currentScene, out HashSet<NetworkIdentity> objects) && objects.Remove(identity))
                    dirtyScenes.Add(currentScene);
            }
        }

        [ServerCallback]
        void LateUpdate()
        {
            // for each spawned:
            //   if scene changed:
            //     add previous to dirty
            //     add new to dirty
            //   else
            //     if rebuild interval reached:
            //       rebuild moved identities / dirty scenes
            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (!lastObjectScene.TryGetValue(identity, out Scene currentScene))
                    continue;

                Scene newScene = identity.gameObject.scene;
                if (newScene != currentScene)
                {
                    // Mark new/old scenes as dirty so they get rebuilt
                    dirtyScenes.Add(currentScene);
                    dirtyScenes.Add(newScene);

                    // This object is in a new scene so observers in the prior scene
                    // and the new scene need to rebuild their respective observers lists.

                    // Remove this object from the hashset of the scene it just left
                    if (sceneObjects.TryGetValue(currentScene, out HashSet<NetworkIdentity> currentObjects))
                        currentObjects.Remove(identity);

                    // Set this to the new scene this object just entered
                    lastObjectScene[identity] = newScene;

                    // Make sure this new scene is in the dictionary
                    if (!sceneObjects.TryGetValue(newScene, out HashSet<NetworkIdentity> newObjects))
                    {
                        newObjects = new HashSet<NetworkIdentity>();
                        sceneObjects.Add(newScene, newObjects);
                    }

                    // Add this object to the hashset of the new scene
                    newObjects.Add(identity);
                }
            }

            if (NetworkTime.localTime >= lastRebuildTime + rebuildInterval)
            {
                RebuildConnectionCache();

                rebuildCounter++;
                bool updateStatic = rebuildCounter >= staticRebuildInterval;
                float minMoveDistanceSq = minMoveDistance * minMoveDistance;

                // Detect player movement. If a connection moved, all identities in that scene
                // may need rebuilding because connections are the observers.
                foreach ((NetworkConnectionToClient conn, Vector3 pos) in cachedConnections)
                {
                    if (conn == null || conn.identity == null)
                        continue;

                    NetworkIdentity connIdentity = conn.identity;
                    if (!lastIdentityPositions.TryGetValue(connIdentity, out Vector3 last) ||
                        (pos - last).sqrMagnitude >= minMoveDistanceSq)
                    {
                        lastIdentityPositions[connIdentity] = pos;
                        dirtyScenes.Add(connIdentity.gameObject.scene);
                    }
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

                if (updateStatic)
                    rebuildCounter = 0;

                lastRebuildTime = NetworkTime.localTime;
            }

            if (dirtyScenes.Count > 0 || dirtyIdentities.Count > 0)
            {
                if (cachedConnections.Count == 0)
                    RebuildConnectionCache();

                // rebuild all dirty scenes
                foreach (Scene dirtyScene in dirtyScenes)
                    RebuildSceneObservers(dirtyScene);

                foreach (NetworkIdentity identity in dirtyIdentities)
                    if (identity != null && !dirtyScenes.Contains(identity.gameObject.scene))
                        NetworkServer.RebuildObservers(identity, false);

                dirtyScenes.Clear();
                dirtyIdentities.Clear();
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

        void RebuildSceneObservers(Scene scene)
        {
            if (!sceneObjects.TryGetValue(scene, out HashSet<NetworkIdentity> objects))
                return;

            foreach (NetworkIdentity netIdentity in objects)
                if (netIdentity != null)
                    NetworkServer.RebuildObservers(netIdentity, false);
        }

        public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnectionToClient newObserver)
        {
            // Check for scene match first, then distance
            if (newObserver == null || newObserver.identity == null)
                return false;

            if (identity.gameObject.scene != newObserver.identity.gameObject.scene)
                return false;

            int range = GetVisRange(identity);
            return (identity.transform.position - newObserver.identity.transform.position).sqrMagnitude < (long)range * range;
        }

        public override void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnectionToClient> newObservers)
        {
            int range = GetVisRange(identity);
            long rangeSq = (long)range * range;
            Vector3 position = identity.transform.position;

            // Use cached connections when available for fewer transform lookups.
            if (cachedConnections.Count > 0)
            {
                foreach ((NetworkConnectionToClient conn, Vector3 connPos) in cachedConnections)
                    if (conn != null && conn.identity != null && conn.identity.gameObject.scene == identity.gameObject.scene &&
                        (connPos - position).sqrMagnitude < rangeSq)
                        newObservers.Add(conn);
            }
            else
            {
                // abort if no entry in sceneObjects yet (created in OnSpawned)
                if (!sceneObjects.TryGetValue(identity.gameObject.scene, out HashSet<NetworkIdentity> objects))
                    return;

                // Add everything in the hashset for this object's current scene if within range
                foreach (NetworkIdentity networkIdentity in objects)
                    if (networkIdentity != null && networkIdentity.connectionToClient != null)
                    {
                        // brute force distance check
                        // -> only player connections can be observers, so it's enough if we
                        //    go through all connections instead of all spawned identities.
                        // -> compared to UNET's sphere cast checking, this one is of orders of
                        //    magnitude faster. if we have 10k monsters and run a sphere
                        //    cast 10k times, we will see a noticeable lag even with physics
                        //    layers. but checking to every connection is fast.
                        NetworkConnectionToClient conn = networkIdentity.connectionToClient;
                        if (conn != null && conn.isAuthenticated && conn.identity != null)
                            if ((conn.identity.transform.position - position).sqrMagnitude < rangeSq)
                                newObservers.Add(conn);
                    }
            }
        }
    }
}
