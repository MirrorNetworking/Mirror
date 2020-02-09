using UnityEngine;
using System.Collections.Generic;

namespace Mirror
{
    /// <summary>
    /// Component that controls visibility of networked objects between scenes.
    /// <para>Any object with this component on it will only be visible to other objects in the same scene</para>
    /// <para>This would be used when the server has multiple additive subscenes loaded to isolate players to their respective subscenes</para>
    /// </summary>
    [AddComponentMenu("Network/NetworkSceneChecker")]
    [RequireComponent(typeof(NetworkIdentity))]
    [HelpURL("https://mirror-networking.com/docs/Components/NetworkSceneChecker.html")]
    public class NetworkSceneChecker : NetworkBehaviour
    {
        /// <summary>
        /// Flag to force this object to be hidden from all observers.
        /// <para>If this object is a player object, it will not be hidden for that client.</para>
        /// </summary>
        [Tooltip("Enable to force this object to be hidden from all observers.")]
        public bool forceHidden;

        public static readonly Dictionary<string, HashSet<NetworkIdentity>> sceneCheckerObjects = new Dictionary<string, HashSet<NetworkIdentity>>();

        string currentScene;

        [ServerCallback]
        void OnEnable()
        {
            currentScene = gameObject.scene.name;
            if (LogFilter.Debug) Debug.Log($"NetworkSceneChecker.OnEnable currentScene: {currentScene}");
        }

        public override void OnStartServer()
        {
            if (!sceneCheckerObjects.ContainsKey(currentScene))
                sceneCheckerObjects.Add(currentScene, new HashSet<NetworkIdentity>());

            sceneCheckerObjects[currentScene].Add(netIdentity);
        }

        [ServerCallback]
        void Update()
        {
            if (currentScene == gameObject.scene.name)
                return;

            // This object is in a new scene so observers in the prior scene
            // and the new scene need to rebuild their respective observers lists.

            // Remove this object from the hashset of the scene it just left
            sceneCheckerObjects[currentScene].Remove(netIdentity);

            // RebuildObservers of all NetworkIdentity's in the scene this object just left
            RebuildSceneObservers();

            // Set this to the new scene this object just entered
            currentScene = gameObject.scene.name;

            // Make sure this new scene is in the dictionary
            if (!sceneCheckerObjects.ContainsKey(currentScene))
                sceneCheckerObjects.Add(currentScene, new HashSet<NetworkIdentity>());

            // Add this object to the hashset of the new scene
            sceneCheckerObjects[currentScene].Add(netIdentity);

            // RebuildObservers of all NetworkIdentity's in the scene this object just entered
            RebuildSceneObservers();
        }

        void RebuildSceneObservers()
        {
            foreach (NetworkIdentity networkIdentity in sceneCheckerObjects[currentScene])
                if (networkIdentity != null)
                    networkIdentity.RebuildObservers(false);
        }

        /// <summary>
        /// Called when a new player enters the scene
        /// </summary>
        /// <param name="newObserver">NetworkConnection of player object</param>
        /// <returns>True if object is in the same scene</returns>
        public override bool OnCheckObserver(NetworkConnection conn)
        {
            if (forceHidden)
                return false;

            return conn.identity.gameObject.scene == gameObject.scene;
        }

        // Always return true when overriding OnRebuildObservers so that
        // Mirror knows not to use the built in rebuild method.
        public override bool OnRebuildObservers(HashSet<NetworkConnection> observers, bool initialize)
        {
            // If forceHidden then return true without adding any observers.
            if (forceHidden)
                return true;

            // Add everything in the hashset for this object's current scene
            foreach (NetworkIdentity networkIdentity in sceneCheckerObjects[currentScene])
                if (networkIdentity != null && networkIdentity.connectionToClient != null)
                    observers.Add(networkIdentity.connectionToClient);

            return true;
        }

        /// <summary>
        /// Called when hiding and showing objects on the host.
        /// On regular clients, objects simply spawn/despawn.
        /// On host, objects need to remain in scene because the host is also the server.
        /// In that case, we simply hide/show meshes for the host player.
        /// </summary>
        /// <param name="visible"></param>
        public override void OnSetHostVisibility(bool visible)
        {
            foreach (Renderer rend in GetComponentsInChildren<Renderer>())
                rend.enabled = visible;
        }
    }
}
