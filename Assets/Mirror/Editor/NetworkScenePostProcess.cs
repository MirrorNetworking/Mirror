using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror
{
    public class NetworkScenePostProcess : MonoBehaviour
    {
        // helper function to check if a NetworkIdentity is in the active scene
        static bool InActiveScene(NetworkIdentity identity) =>
            identity.gameObject.scene == SceneManager.GetActiveScene();

        [PostProcessScene]
        public static void OnPostProcessScene()
        {
            // find all NetworkIdentities in this scene
            // => but really only from this scene. this avoids weird situations
            //    like in NetworkZones when we destroy the local player and
            //    load another scene afterwards, yet the local player is still
            //    in the FindObjectsOfType result with scene=DontDestroyOnLoad
            //    for some reason
            foreach (NetworkIdentity identity in FindObjectsOfType<NetworkIdentity>().Where(InActiveScene))
            {
                // if we had a [ConflictComponent] attribute that would be better than this check.
                // also there is no context about which scene this is in.
                if (identity.GetComponent<NetworkManager>() != null)
                {
                    Debug.LogError("NetworkManager has a NetworkIdentity component. This will cause the NetworkManager object to be disabled, so it is not recommended.");
                }
                if (identity.isClient || identity.isServer)
                    continue;

                // check if the sceneId was set properly
                // it might not if the scene wasn't opened with new sceneId
                // Mirror version yet.
                // => show error for each object where this applies.
                //    the user should get lots of errors to notice it!
                // => throwing an exception would only show it for one object
                //    because this function would return afterwards.
                if (identity.sceneId != 0)
                {
                    // set scene id build index byte
                    identity.SetSceneIdSceneIndexByteInternal();
                }
                else Debug.LogError(identity.name + "'s sceneId wasn't generated yet. This can happen if a scene was last saved with an older version of Mirror. Please open the scene " + identity.gameObject.scene.name + " and click on the " + identity.name + " object in the Hierarchy once, so that OnValidate is called and the sceneId is set. Afterwards resave the scene.");

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
        }
    }
}
