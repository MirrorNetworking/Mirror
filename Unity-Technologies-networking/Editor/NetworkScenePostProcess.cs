#if ENABLE_UNET
using System.Collections.Generic;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.Networking;

namespace UnityEditor
{
    public class NetworkScenePostProcess : MonoBehaviour
    {
        [PostProcessScene]
        public static void OnPostProcessScene()
        {
            var prefabWarnings = new HashSet<string>();

            int nextSceneId = 1;
            foreach (NetworkIdentity uv in FindObjectsOfType<NetworkIdentity>())
            {
                // if we had a [ConflictComponent] attribute that would be better than this check.
                // also there is no context about which scene this is in.
                if (uv.GetComponent<NetworkManager>() != null)
                {
                    Debug.LogError("NetworkManager has a NetworkIdentity component. This will cause the NetworkManager object to be disabled, so it is not recommended.");
                }
                if (uv.isClient || uv.isServer)
                    continue;

                uv.gameObject.SetActive(false);
                uv.ForceSceneId(nextSceneId++);

                var prefabGO = UnityEditor.PrefabUtility.GetPrefabParent(uv.gameObject) as GameObject;
                if (prefabGO)
                {
                    var prefabRootGO = UnityEditor.PrefabUtility.FindPrefabRoot(prefabGO);
                    if (prefabRootGO)
                    {
                        var identities = prefabRootGO.GetComponentsInChildren<NetworkIdentity>();
                        if (identities.Length > 1 && !prefabWarnings.Contains(prefabRootGO.name))
                        {
                            // make sure we only print one error per prefab
                            prefabWarnings.Add(prefabRootGO.name);

                            Debug.LogWarningFormat("Prefab '{0}' has several NetworkIdentity components attached to itself or its children, this is not supported.", prefabRootGO.name);
                        }
                    }
                }
            }
        }
    }
}
#endif //ENABLE_UNET
