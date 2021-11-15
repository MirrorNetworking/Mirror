using UnityEngine;

namespace Mirror.Examples.AdditiveScenes
{
    // This script is attached to a prefab called Zone that is on the Player layer
    // AdditiveNetworkManager, in OnStartServer, instantiates the prefab only on the server.
    // It never exists for clients (other than host client if there is one).
    // The prefab has a Sphere Collider with isTrigger = true.
    // These OnTrigger events only run on the server and will only send a message to the
    // client that entered the Zone to load the subscene assigned to the subscene property.
    public class ZoneHandler : MonoBehaviour
    {
        [Scene]
        [Tooltip("Assign the sub-scene to load for this zone")]
        public string subScene;

        [ServerCallback]
        void OnTriggerEnter(Collider other)
        {
            // Debug.Log($"Loading {subScene}");

            NetworkIdentity networkIdentity = other.gameObject.GetComponent<NetworkIdentity>();
            SceneMessage message = new SceneMessage{ sceneName = subScene, sceneOperation = SceneOperation.LoadAdditive };
            networkIdentity.connectionToClient.Send(message);
        }

        [ServerCallback]
        void OnTriggerExit(Collider other)
        {
            // Debug.Log($"Unloading {subScene}");

            NetworkIdentity networkIdentity = other.gameObject.GetComponent<NetworkIdentity>();
            SceneMessage message = new SceneMessage{ sceneName = subScene, sceneOperation = SceneOperation.UnloadAdditive };
            networkIdentity.connectionToClient.Send(message);
        }
    }
}
