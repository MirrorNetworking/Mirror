using UnityEngine;

namespace Mirror.Examples.Additive
{
    // This script is attached to a scene object called Zone that is on the Player layer and has:
    // - Sphere Collider with isTrigger = true
    // - Network Identity with Server Only checked
    // These OnTrigger events only run on the server and will only send a message to the player
    // that entered the Zone to load the subscene assigned to the subscene property.
    public class ZoneHandler : NetworkBehaviour
    {
        [Scene]
        [Tooltip("Assign the sub-scene to load for this zone")]
        public string subScene;

        [Server]
        void OnTriggerEnter(Collider other)
        {
            Debug.LogFormat("Loading {0}", subScene);

            // Get a reference to the SceneLoader component on the player prefab
            SceneLoader sceneLoader = other.gameObject.GetComponent<SceneLoader>();

            NetworkIdentity networkIdentity = other.gameObject.GetComponent<NetworkIdentity>();

            // One or both of these might be null if you don't have Layers set up properly
            if (sceneLoader != null && networkIdentity != null)
                sceneLoader.TargetLoadUnloadScene(networkIdentity.connectionToClient, subScene, SceneLoader.LoadAction.Load);
        }

        [Server]
        void OnTriggerExit(Collider other)
        {
            Debug.LogFormat("Unloading {0}", subScene);

            // Get a reference to the SceneLoader component on the player prefab
            SceneLoader sceneLoader = other.gameObject.GetComponent<SceneLoader>();

            NetworkIdentity networkIdentity = other.gameObject.GetComponent<NetworkIdentity>();

            // One or both of these might be null if you don't have Layers set up properly
            if (sceneLoader != null && networkIdentity != null)
                sceneLoader.TargetLoadUnloadScene(networkIdentity.connectionToClient, subScene, SceneLoader.LoadAction.Unload);
        }
    }
}
