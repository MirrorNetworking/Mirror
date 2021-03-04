using UnityEngine;

namespace Mirror.Examples.Additive
{
    // This script is attached to a scene object called Zone that is on the Player layer and has:
    // - Sphere Collider with isTrigger = true
    // - Network Identity with Server Only checked
    // These OnTrigger events only run on the server and will only send a message to the player
    // that entered the Zone to load the subscene assigned to the subscene property.
    public class ZoneHandler : MonoBehaviour
    {
        [Scene]
        [Tooltip("Assign the sub-scene to load for this zone")]
        public string subScene;

        void OnTriggerEnter(Collider other)
        {
            if (!NetworkServer.active) return;

            // Debug.LogFormat(LogType.Log, "Loading {0}", subScene);

            NetworkIdentity networkIdentity = other.gameObject.GetComponent<NetworkIdentity>();
            SceneMessage message = new SceneMessage{ sceneName = subScene, sceneOperation = SceneOperation.LoadAdditive };
            networkIdentity.connectionToClient.Send(message);
        }

        void OnTriggerExit(Collider other)
        {
            if (!NetworkServer.active) return;

            // Debug.LogFormat(LogType.Log, "Unloading {0}", subScene);

            NetworkIdentity networkIdentity = other.gameObject.GetComponent<NetworkIdentity>();
            SceneMessage message = new SceneMessage{ sceneName = subScene, sceneOperation = SceneOperation.UnloadAdditive };
            networkIdentity.connectionToClient.Send(message);
        }
    }
}
