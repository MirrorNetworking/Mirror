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
        static readonly ILogger logger = LogFactory.GetLogger(typeof(ZoneHandler));

        [Scene]
        [Tooltip("Assign the sub-scene to load for this zone")]
        public string subScene;

        void OnTriggerEnter(Collider other)
        {
            if (!NetworkServer.active) return;

            if (logger.LogEnabled()) logger.LogFormat(LogType.Log, "Loading {0}", subScene);

            NetworkIdentity networkIdentity = other.gameObject.GetComponent<NetworkIdentity>();
            NetworkServer.SendToClientOfPlayer(networkIdentity, new SceneMessage { sceneName = subScene, sceneOperation = SceneOperation.LoadAdditive });
        }

        void OnTriggerExit(Collider other)
        {
            if (!NetworkServer.active) return;

            if (logger.LogEnabled()) logger.LogFormat(LogType.Log, "Unloading {0}", subScene);

            NetworkIdentity networkIdentity = other.gameObject.GetComponent<NetworkIdentity>();
            NetworkServer.SendToClientOfPlayer(networkIdentity, new SceneMessage { sceneName = subScene, sceneOperation = SceneOperation.UnloadAdditive });
        }
    }
}
