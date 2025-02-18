using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror
{
    [AddComponentMenu("Network/ Interest Management/ Scene/Scene Distance Interest Management")]
    public class SceneDistanceInterestManagement : InterestManagement
    {
        [Tooltip("The maximum range that objects will be visible at. Add DistanceInterestManagementCustomRange onto NetworkIdentities for custom ranges.")]
        public int visRange = 500;

        [Tooltip("Rebuild all every 'rebuildInterval' seconds.")]
        public float rebuildInterval = 1;
        double lastRebuildTime;

        // cache custom ranges to avoid runtime TryGetComponent lookups
        readonly Dictionary<NetworkIdentity, DistanceInterestManagementCustomRange> CustomRanges = new Dictionary<NetworkIdentity, DistanceInterestManagementCustomRange>();

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
        }

        // Use Scene instead of string scene.name because when additively
        // loading multiples of a subscene the name won't be unique
        readonly Dictionary<Scene, HashSet<NetworkIdentity>> sceneObjects =
            new Dictionary<Scene, HashSet<NetworkIdentity>>();

        readonly Dictionary<NetworkIdentity, Scene> lastObjectScene =
            new Dictionary<NetworkIdentity, Scene>();

        HashSet<Scene> dirtyScenes = new HashSet<Scene>();

        [ServerCallback]
        public override void OnSpawned(NetworkIdentity identity)
        {
            if (identity.TryGetComponent(out DistanceInterestManagementCustomRange custom))
                CustomRanges[identity] = custom;

            Scene currentScene = identity.gameObject.scene;
            lastObjectScene[identity] = currentScene;
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
            //       rebuild all
            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                if (!lastObjectScene.TryGetValue(identity, out Scene currentScene))
                    continue;

                Scene newScene = identity.gameObject.scene;
                if (newScene == currentScene)
                {
                    if (NetworkTime.localTime >= lastRebuildTime + rebuildInterval)
                    {
                        RebuildAll();
                        lastRebuildTime = NetworkTime.localTime;
                    }

                    // no scene change, so we're done here
                    continue;
                }

                // Mark new/old scenes as dirty so they get rebuilt
                dirtyScenes.Add(currentScene);
                dirtyScenes.Add(newScene);

                // This object is in a new scene so observers in the prior scene
                // and the new scene need to rebuild their respective observers lists.

                // Remove this object from the hashset of the scene it just left
                sceneObjects[currentScene].Remove(identity);

                // Set this to the new scene this object just entered
                lastObjectScene[identity] = newScene;

                // Make sure this new scene is in the dictionary
                if (!sceneObjects.ContainsKey(newScene))
                    sceneObjects.Add(newScene, new HashSet<NetworkIdentity>());

                // Add this object to the hashset of the new scene
                sceneObjects[newScene].Add(identity);
            }

            // rebuild all dirty scenes
            foreach (Scene dirtyScene in dirtyScenes)
                RebuildSceneObservers(dirtyScene);

            dirtyScenes.Clear();
        }

        void RebuildSceneObservers(Scene scene)
        {
            foreach (NetworkIdentity netIdentity in sceneObjects[scene])
                if (netIdentity != null)
                    NetworkServer.RebuildObservers(netIdentity, false);
        }

        public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnectionToClient newObserver)
        {
            // Check for scene match first, then distance
            if (identity.gameObject.scene != newObserver.identity.gameObject.scene) return false;

            int range = GetVisRange(identity);
            return Vector3.Distance(identity.transform.position, newObserver.identity.transform.position) < range;
        }

        public override void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnectionToClient> newObservers)
        {
            // abort if no entry in sceneObjects yet (created in OnSpawned)
            if (!sceneObjects.TryGetValue(identity.gameObject.scene, out HashSet<NetworkIdentity> objects))
                return;

            int range = GetVisRange(identity);
            Vector3 position = identity.transform.position;

            // Add everything in the hashset for this object's current scene if within range
            foreach (NetworkIdentity networkIdentity in objects)
                if (networkIdentity != null && networkIdentity.connectionToClient != null)
                {
                    // brute force distance check
                    // -> only player connections can be observers, so it's enough if we
                    //    go through all connections instead of all spawned identities.
                    // -> compared to UNET's sphere cast checking, this one is orders of
                    //    magnitude faster. if we have 10k monsters and run a sphere
                    //    cast 10k times, we will see a noticeable lag even with physics
                    //    layers. but checking to every connection is fast.
                    NetworkConnectionToClient conn = networkIdentity.connectionToClient;
                    if (conn != null && conn.isAuthenticated && conn.identity != null)
                        if (Vector3.Distance(conn.identity.transform.position, position) < range)
                            newObservers.Add(conn);
                }
        }
    }
}
