using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

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
        /// How often (in seconds) that this object should update the list of observers that can see it.
        /// </summary>
        [Tooltip("How often (in seconds) that this object should check for scene change. Set to zero to disable.")]
        public float updateInterval = 1;

        /// <summary>
        /// Flag to force this object to be hidden from all observers.
        /// <para>If this object is a player object, it will not be hidden for that client.</para>
        /// </summary>
        [Tooltip("Enable to force this object to be hidden from all observers.")]
        public bool forceHidden;

        float lastUpdateTime;

        Scene currentScene;

        public override void OnStartClient()
        {
            base.OnStartClient();
            Debug.LogError($"OnStartClient A:{gameObject.name} B:{isLocalPlayer} C:{currentScene.name} D:{NetworkClient.connection.identity.gameObject.scene.name}");

            if (isLocalPlayer) return;

            if (currentScene == null) return;

            if (currentScene == NetworkClient.connection.identity.gameObject.scene) return;

            SceneManager.MoveGameObjectToScene(gameObject, NetworkClient.connection.identity.gameObject.scene);
        }

        void Update()
        {
            if (!NetworkServer.active || updateInterval == 0)
                return;

            if (currentScene == null)
                currentScene = gameObject.scene;

            if (Time.time - lastUpdateTime > updateInterval)
            {
                if (currentScene != gameObject.scene)
                {
                    // This object is in a new scene so potential observers in the prior scene
                    // and the new scene need to rebuild their respective observers lists.
                    foreach (NetworkIdentity networkIdentity in NetworkIdentity.spawned.Values)
                    {
                        Scene objectScene = networkIdentity.gameObject.scene;
                        if (objectScene == currentScene || objectScene == gameObject.scene)
                            networkIdentity.RebuildObservers(false);
                    }

                    currentScene = gameObject.scene;
                }

                lastUpdateTime = Time.time;
            }
        }

        public override bool OnCheckObserver(NetworkConnection conn)
        {
            if (forceHidden)
                return false;

            return (conn.identity.gameObject.scene == gameObject.scene);
        }

        public override bool OnRebuildObservers(HashSet<NetworkConnection> observers, bool initialize)
        {
            // if force hidden then return without adding any observers.
            if (forceHidden)
                // always return true when overwriting OnRebuildObservers so that
                // Mirror knows not to use the built in rebuild method.
                return true;

            foreach (NetworkConnection conn in NetworkServer.connections.Values)
            {
                if (conn.identity.gameObject.scene == gameObject.scene)
                    observers.Add(conn);
            }

            // always return true when overwriting OnRebuildObservers so that
            // Mirror knows not to use the built in rebuild method.
            return true;
        }

        /// <summary>
        /// Called when hiding and showing objects on the host.
        /// On regular clients, objects simply spawn/despawn.
        /// On host, objects need to remain in scene because the host is also the server.
        ///    In that case, we simply hide/show meshes for the host player.
        /// </summary>
        /// <param name="visible"></param>
        public override void OnSetHostVisibility(bool visible)
        {
            foreach (Renderer rend in GetComponentsInChildren<Renderer>())
            {
                rend.enabled = visible;
            }
        }
    }
}
