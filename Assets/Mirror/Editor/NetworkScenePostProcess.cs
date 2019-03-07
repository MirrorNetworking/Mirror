using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror
{
    public class NetworkScenePostProcess : MonoBehaviour
    {
        [PostProcessScene]
        public static void OnPostProcessScene()
        {
            List<NetworkIdentity> identities = FindObjectsOfType<NetworkIdentity>().ToList();

            // sceneId assignments need to work with additive scene loading, so
            // it can't always start at 1,2,3,4,..., otherwise there will be
            // sceneId duplicates.
            // -> we need an offset to start at 1000+1,+2,+3, etc.
            // -> the most robust way is to split uint value range by sceneCount
            // -> only if more than one scene. otherwise use offset 0 to avoid
            //    DivisionByZero if no scene in build settings, and to avoid
            //    different offsets in editor/build if scene wasn't added to
            //    build settings.
            uint offsetPerScene = 0;
            if (SceneManager.sceneCountInBuildSettings > 1)
            {
                // make sure that there aren't more sceneIds than offsetPerScene
                // -> only if we have multiple scenes. otherwise offset is 0, in
                //    which case it doesn't matter.
                if (identities.Count >= NetworkIdentity.OffsetPerScene)
                {
                    Debug.LogWarning(">=" + offsetPerScene + " NetworkIdentities in scene. Additive scene loading will cause duplicate ids.");
                }
            }

            foreach (NetworkIdentity identity in identities)
            {
                // if we had a [ConflictComponent] attribute that would be better than this check.
                // also there is no context about which scene this is in.
                if (identity.GetComponent<NetworkManager>() != null)
                {
                    Debug.LogError("NetworkManager has a NetworkIdentity component. This will cause the NetworkManager object to be disabled, so it is not recommended.");
                }
                if (identity.isClient || identity.isServer)
                    continue;

                uint newId = ((uint)identity.gameObject.scene.buildIndex * NetworkIdentity.OffsetPerScene) + identity.sceneId;
                if (LogFilter.Debug)
                    Debug.LogFormat("[NetworkScenePostProcess] SceneId: {0} => {1} Path: {2}",  identity.sceneId, newId, identity.gameObject.GetHierarchyPath());
                identity.ForceSceneId(newId);

                // disable it AFTER assigning the sceneId.
                // -> this way NetworkIdentity.OnDisable adds itself to the
                //    spawnableObjects dictionary (only if sceneId != 0)
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

            // We do this here to ensure that none of the build scene ID's contaminate the scene when it opens afterwards
            NetworkIdentityManager.Instance.Clear();
        }
    }
}
