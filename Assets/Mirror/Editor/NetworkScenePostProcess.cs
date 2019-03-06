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

        // persistent sceneId assignment to fix readstring bug that occurs when
        // restarting the editor and connecting to a build again. sceneids were
        // then different because FindObjectsOfType's order is not guaranteed to
        // be the same.
        // -> we need something unique and persistent, aka always the same when
        //    pressing play/building the first time
        //
        // sceneId challenges/requirements:
        // * it needs to be 0 for prefabs
        //   => we set it to 0 in NetworkIdentity.SetupIDs() if prefab!
        // * it needs to be only assigned to objects that were in the scene
        //   since the beginning
        //   => OnPostProcessScene is only called once on load
        // * there can be no duplicate ids
        //   => Unity's fileID is unique
        //   => fileID + sceneHash is potentially not unique across scenes, but
        //      that risk is very small. we could still store fileId, hash in a
        //      64 bit long if needed later.
        // * duplicating the whole scene file should result in different
        //   sceneIds for the files in the duplicated scene, even if that file
        //   was never opened yet (e.g. when upgrading to Mirror)
        //   => adding hash(scene.name) makes the fileIDs unique across scenes.
        //   => this even works if the scene was never opened before, because
        //      Unity calls OnPostProcessScene for all scenes when they are
        //      built or opened
        // * it needs to work with scenes that may or may not be in build index
        //   and may or may not be enabled/disabled there.
        //   => hash(scene.name) doesn't care
        // * Ids need to be deterministic or saved if randomly generated
        //   => saving ids is always difficult and full of edge cases
        //   => generated a deterministic id in OnPostProcessScene is the
        //      perfect(!) fail safe solution. doing this in OnValidate would be
        //      very difficult to get right, especially for scenes that were
        //      never opened (where OnValidate wasn't called yet).
        static uint CalculateDeterministicSceneId(NetworkIdentity identity)
        {
            // assign sceneId to hash(scene) + fileID
            uint sceneHash = (uint)identity.gameObject.scene.name.GetStableHashCode();
            uint fileID = GetFileID(identity);
            return sceneHash + fileID;
        }

        [PostProcessScene]
        public static void OnPostProcessScene()
        {
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

                // vis2k: MISMATCHING SCENEID BUG FIX
                // problem:
                //   * FindObjectsOfType order is not guaranteed. restarting the
                //     editor results in a different order, so we can't assign a
                //     counter.
                //   * connecting to a build again would cause Mirror to deserialize
                //     the wrong objects, causing all kinds of weird errors like
                //     'ReadString out of range'
                //
                // solution: use a deterministic scene id
                uint sceneId = CalculateDeterministicSceneId(identity);
                identity.ForceSceneId(sceneId);
                if (LogFilter.Debug) Debug.Log("PostProcess sceneId assigned: name=" + identity.name + " scene=" + identity.gameObject.scene.name + " sceneid=" + identity.sceneId);

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
