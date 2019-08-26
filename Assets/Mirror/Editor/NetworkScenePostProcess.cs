using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Mirror
{
    public class NetworkScenePostProcess : MonoBehaviour
    {
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
                        // set scene hash
                        identity.SetSceneIdSceneHashPartInternal();

                        // disable it
                        // note: NetworkIdentity.OnDisable adds itself to the
                        //       spawnableObjects dictionary (only if sceneId != 0)
                        identity.gameObject.SetActive(false);

                        // safety check for prefabs with more than one NetworkIdentity
        #if UNITY_2018_2_OR_NEWER
                        GameObject prefabGO = PrefabUtility.GetCorrespondingObjectFromSource(identity.gameObject) as GameObject;
        #else
                        GameObject prefabGO = PrefabUtility.GetPrefabParent(identity.gameObject) as GameObject;
        #endif
                        if (prefabGO)
                        {
        #if UNITY_2018_3_OR_NEWER
                            GameObject prefabRootGO = prefabGO.transform.root.gameObject;
        #else
                            GameObject prefabRootGO = PrefabUtility.FindPrefabRoot(prefabGO);
        #endif
                            if (prefabRootGO)
                            {
                                if (prefabRootGO.GetComponentsInChildren<NetworkIdentity>().Length > 1)
                                {
                                    Debug.LogWarningFormat("Prefab '{0}' has several NetworkIdentity components attached to itself or its children, this is not supported.", prefabRootGO.name);
                                }
                            }
                        }
                    }
                    // throwing an exception would only show it for one object
                    // because this function would return afterwards.
                    else Debug.LogError("Scene " + identity.gameObject.scene.path + " needs to be opened and resaved, because the scene object " + identity.name + " has no valid sceneId yet.");
                }
            }
        }
    }
}
