using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Mirror
{
    public class SceneInterestManagement : InterestManagement
    {
        // Use Scene instead of string scene.name because when additively
        // loading multiples of a subscene the name won't be unique
        readonly Dictionary<Scene, HashSet<NetworkIdentity>> sceneObjects =
            new Dictionary<Scene, HashSet<NetworkIdentity>>();

        readonly Dictionary<NetworkIdentity, Scene> lastObjectScene =
            new Dictionary<NetworkIdentity, Scene>();

        HashSet<Scene> dirtyScenes = new HashSet<Scene>();

        public override void OnSpawned(NetworkIdentity identity)
        {
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

        public override void OnDestroyed(NetworkIdentity identity)
        {
            Scene currentScene = lastObjectScene[identity];
            lastObjectScene.Remove(identity);
            if (sceneObjects.TryGetValue(currentScene, out HashSet<NetworkIdentity> objects) && objects.Remove(identity))
                RebuildSceneObservers(currentScene);
        }

        // internal so we can update from tests
        [ServerCallback]
        internal void Update()
        {
            // for each spawned:
            //   if scene changed:
            //     add previous to dirty
            //     add new to dirty
            foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
            {
                Scene currentScene = lastObjectScene[identity];
                Scene newScene = identity.gameObject.scene;
                if (newScene == currentScene) 
                    continue;

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

        public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnection newObserver)
        {
            return identity.gameObject.scene == newObserver.identity.gameObject.scene;
        }

        public override void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnection> newObservers, bool initialize)
        {
            if (!sceneObjects.TryGetValue(identity.gameObject.scene, out HashSet<NetworkIdentity> objects))
                return;

            // Add everything in the hashset for this object's current scene
            foreach (NetworkIdentity networkIdentity in objects)
                if (networkIdentity != null && networkIdentity.connectionToClient != null)
                    newObservers.Add(networkIdentity.connectionToClient);
        }
    }
}
