using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Mirror
{
    public class NetworkScenePostProcess : MonoBehaviour
    {
        // Coburn: This runs to ensure people haven't done things like
        // attempting to put a NetworkIdentity on a NetworkManager, or
        // NetworkBehaviours on a NetworkManager.
        [PostProcessScene]
        public static void DetectWrongUsageOfNetworkBehavioursAndIdentities()
        {
            IEnumerable<NetworkManager> netManagers = Resources.FindObjectsOfTypeAll<NetworkManager>()
                .Where(netManager => netManager.gameObject.hideFlags != HideFlags.NotEditable &&
                                   netManager.gameObject.hideFlags != HideFlags.HideAndDontSave &&
                                   netManager.gameObject.scene.name != "DontDestroyOnLoad" &&
                                   !PrefabUtility.IsPartOfPrefabAsset(netManager.gameObject));

            int netManCount = netManagers.Count();
            if (netManCount > 0)
            {
                if (netManCount > 1)
                {
                    // Very bad idea to have more than one NetworkManager.
                    Debug.LogError("Multiple NetworkManagers detected in scene, this may cause problems!");
                }

                foreach (NetworkManager netManager in netManagers)
                {
                    // NetworkBehaviour check.
                    NetworkBehaviour[] checkForNetBehaviours = netManager.gameObject.GetComponentsInChildren<NetworkBehaviour>();
                    if (checkForNetBehaviours.Length > 0)
                    {
                        // Throw an error saying that this is not supported.
                        Debug.LogError("Detected one or more NetworkBehaviours on the same GameObject as the NetworkManager. NetworkBehaviours should never be added to a " +
                            $"GameObject with the NetworkManager script attached. Remove these components from the '{netManager.gameObject.name}' GameObject.");
                    }

                    // NetworkIdentity check.
                    NetworkIdentity[] checkForNetIdentities = netManager.gameObject.GetComponentsInChildren<NetworkIdentity>();
                    if (checkForNetIdentities.Length > 0)
                    {
                        // Throw an error saying this will cause problems. Mirror later checks to see if the NetworkManager has a NetworkIdentity, but this one pin-points the issue.
                        Debug.LogError("Detected a NetworkIdentity on the same GameObject as the NetworkManager. A NetworkIdentity should never be added to a " +
                            $"GameObject with the NetworkManager script attached. Remove this component from the '{netManager.gameObject.name}' GameObject.");
                    }
                }
            }
        }

        [PostProcessScene]
        public static void OnPostProcessScene()
        {
            // find all NetworkIdentities in all scenes
            // => can't limit it to GetActiveScene() because that wouldn't work
            //    for additive scene loads (the additively loaded scene is never
            //    the active scene)
            // => ignore DontDestroyOnLoad scene! this avoids weird situations
            //    like in NetworkZones when we destroy the local player and
            //    load another scene afterwards, yet the local player is still
            //    in the FindObjectsOfType result with scene=DontDestroyOnLoad
            //    for some reason
            // => OfTypeAll so disabled objects are included too
            // => Unity 2019 returns prefabs here too, so filter them out.
            IEnumerable<NetworkIdentity> identities = Resources.FindObjectsOfTypeAll<NetworkIdentity>()
                .Where(identity => identity.gameObject.hideFlags != HideFlags.NotEditable &&
                                   identity.gameObject.hideFlags != HideFlags.HideAndDontSave &&
                                   identity.gameObject.scene.name != "DontDestroyOnLoad" &&
                                   !PrefabUtility.IsPartOfPrefabAsset(identity.gameObject));

            foreach (NetworkIdentity identity in identities)
            {
                // if we had a [ConflictComponent] attribute that would be better than this check.
                // also there is no context about which scene this is in.
                if (identity.GetComponent<NetworkManager>() != null)
                {
                    Debug.LogError("NetworkManager has a NetworkIdentity component. This will cause the NetworkManager object to be disabled, so it is not recommended.");
                }

                // not spawned before?
                //  OnPostProcessScene is called after additive scene loads too,
                //  and we don't want to set main scene's objects inactive again
                if (!identity.isClient && !identity.isServer)
                {
                    // valid scene object?
                    //   otherwise it might be an unopened scene that still has null
                    //   sceneIds. builds are interrupted if they contain 0 sceneIds,
                    //   but it's still possible that we call LoadScene in Editor
                    //   for a previously unopened scene.
                    //   (and only do SetActive if this was actually a scene object)
                    if (identity.sceneId != 0)
                    {
                        PrepareSceneObject(identity);
                    }
                    // throwing an exception would only show it for one object
                    // because this function would return afterwards.
                    else Debug.LogError("Scene " + identity.gameObject.scene.path + " needs to be opened and resaved, because the scene object " + identity.name + " has no valid sceneId yet.");
                }
            }
        }

        static void PrepareSceneObject(NetworkIdentity identity)
        {
            // set scene hash
            identity.SetSceneIdSceneHashPartInternal();

            // disable it
            // note: NetworkIdentity.OnDisable adds itself to the
            //       spawnableObjects dictionary (only if sceneId != 0)
            identity.gameObject.SetActive(false);

            // safety check for prefabs with more than one NetworkIdentity
#if UNITY_2018_2_OR_NEWER
            GameObject prefabGO = PrefabUtility.GetCorrespondingObjectFromSource(identity.gameObject);
#else
                        GameObject prefabGO = PrefabUtility.GetPrefabParent(identity.gameObject);
#endif
            if (prefabGO)
            {
#if UNITY_2018_3_OR_NEWER
                GameObject prefabRootGO = prefabGO.transform.root.gameObject;
#else
                            GameObject prefabRootGO = PrefabUtility.FindPrefabRoot(prefabGO);
#endif
                if (prefabRootGO != null && prefabRootGO.GetComponentsInChildren<NetworkIdentity>().Length > 1)
                {
                    Debug.LogWarningFormat("Prefab '{0}' has several NetworkIdentity components attached to itself or its children, this is not supported.", prefabRootGO.name);
                }
            }
        }
    }
}
