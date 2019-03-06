using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Mirror
{
    public class NetworkScenePostProcess : MonoBehaviour
    {
        // Unity has a persistent 'fileID' for all GameObjects & components.
        // -> we can see it in Inspector Debug View as 'Local Identifier in File'
        //  -> the only way to access it is via SerializedObject
        //     (https://forum.unity.com/threads/how-to-get-the-local-identifier-in-file-for-scene-objects.265686/)
        static uint GetFileID(UnityEngine.Object obj)
        {
            SerializedObject serializedObject = new SerializedObject(obj);
            PropertyInfo inspectorModeInfo = typeof(SerializedObject).GetProperty("inspectorMode", BindingFlags.NonPublic | BindingFlags.Instance);
            inspectorModeInfo.SetValue(serializedObject, InspectorMode.Debug, null);
            SerializedProperty localIdProp = serializedObject.FindProperty("m_LocalIdentfierInFile"); // note the misspelling!
            return (uint)localIdProp.intValue;
        }

        // we might have inactive scenes in the Editor's build settings, which
        // aren't actually included in builds.
        // so we have to only count the active ones when in Editor, otherwise
        // editor and build sceneIds might get out of sync.
        public static int GetSceneCount()
        {
#if UNITY_EDITOR
            return EditorBuildSettings.scenes.Count(scene => scene.enabled);
#else
            return SceneManager.sceneCountInBuildSettings;
#endif
        }

        [PostProcessScene]
        public static void OnPostProcessScene()
        {
            // vis2k: MISMATCHING SCENEID BUG FIX
            // problem:
            //   * FindObjectsOfType order is not guaranteed. restarting the
            //     editor results in a different order
            //   * connecting to a build again would cause Mirror to deserialize
            //     the wrong objects, causing all kinds of weird errors like
            //     'ReadString out of range'
            //
            // solution: use a persistent scene id (see GetFileID())
            foreach (NetworkIdentity identity in FindObjectsOfType<NetworkIdentity>())
            {
                // if we had a [ConflictComponent] attribute that would be better than this check.
                // also there is no context about which scene this is in.
                if (identity.GetComponent<NetworkManager>() != null)
                {
                    Debug.LogError("NetworkManager has a NetworkIdentity component. This will cause the NetworkManager object to be disabled, so it is not recommended.");
                }
                if (identity.isClient || identity.isServer)
                    continue;

                // assign sceneId to fileID
                // TODO add scene index into it somehow (has to work with disabled too)
                uint fileID = GetFileID(identity);
                identity.ForceSceneId(fileID);
                if (LogFilter.Debug) Debug.Log("PostProcess sceneid assigned: name=" + identity.name + " scene=" + identity.gameObject.scene.name + " sceneid=" + identity.sceneId);

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
        }
    }
}
