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
        [Tooltip("Assign the sub-scene to load for this zone")]
        public SceneField subSceneField;

        [HideInInspector, System.Obsolete("Use subSceneField Instead")]
        public string subScene;

        public void OnValidate()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            SceneFieldFixer.FixField(this, nameof(subScene), nameof(subSceneField));
#pragma warning restore CS0618 // Type or member is obsolete
        }

        [Server]
        void OnTriggerEnter(Collider other)
        {
            Debug.LogFormat("Loading {0}", subSceneField.Path);

            NetworkIdentity networkIdentity = other.gameObject.GetComponent<NetworkIdentity>();
            NetworkServer.SendToClientOfPlayer(networkIdentity, new SceneMessage { sceneName = subSceneField.Path, sceneOperation = SceneOperation.LoadAdditive });
        }

        [Server]
        void OnTriggerExit(Collider other)
        {
            Debug.LogFormat("Unloading {0}", subSceneField.Path);

            NetworkIdentity networkIdentity = other.gameObject.GetComponent<NetworkIdentity>();
            NetworkServer.SendToClientOfPlayer(networkIdentity, new SceneMessage { sceneName = subSceneField.Path, sceneOperation = SceneOperation.UnloadAdditive });
        }
    }
}
