using System.Text;
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
        // persistent sceneId assignment to fix readstring bug that occurs when restarting the editor and
        // connecting to a build again. sceneids were then different because FindObjectsOfType's order
        // is not guranteed to be the same.
        // -> we need something unique and persistent, aka always the same when pressing play/building the first time
        // -> Unity has no built in unique id for GameObjects in the scene

        // helper function to figure out a unique, persistent scene id for a GameObject in the hierarchy
        // -> Unity's instanceId is unique but not persistent
        // -> hashing the whole GameObject is not enough either since a duplicate would have the same hash
        // -> we definitely need the transform sibling index in the hierarchy
        // -> so we might as well use just that
        // -> transforms have children too so we need a list of sibling indices like 0->3->5
        public static List<int> SiblingPathFor(Transform t)
        {
            List<int> result = new List<int>();
            while (t != null)
            {
                result.Add(t.GetSiblingIndex());
                t = t.parent;
            }

            result.Reverse(); // parent to child instead of child to parent order
            return result;
        }

        // we need to compare by using the whole sibling list
        // comparing the string won't work work because:
        //  "1->2"
        //  "20->2"
        // would compare '1' vs '2', then '-' vs '0'
        //
        // tests:
        //   CompareSiblingPaths(new List<int>(){0}, new List<int>(){0}) => 0
        //   CompareSiblingPaths(new List<int>(){0}, new List<int>(){1}) => -1
        //   CompareSiblingPaths(new List<int>(){1}, new List<int>(){0}) => 1
        //   CompareSiblingPaths(new List<int>(){0,1}, new List<int>(){0,2}) => -1
        //   CompareSiblingPaths(new List<int>(){0,2}, new List<int>(){0,1}) => 1
        //   CompareSiblingPaths(new List<int>(){1}, new List<int>(){0,1}) => 1
        //   CompareSiblingPaths(new List<int>(){1}, new List<int>(){2,1}) => -1
        public static int CompareSiblingPaths(List<int> left, List<int> right)
        {
            // compare [0], remove it, compare next, etc.
            while (left.Count > 0 && right.Count > 0)
            {
                if (left[0] < right[0])
                {
                    return -1;
                }
                else if (left[0] > right[0])
                {
                    return 1;
                }
                else
                {
                    // equal, so they are both children of the same transform
                    // -> which also means that they both must have one more
                    //    entry, so we can remove both without checking size
                    left.RemoveAt(0);
                    right.RemoveAt(0);
                }
            }

            // equal if both were empty or both had the same entry without any
            // more children (should never happen in practice)
            return 0;
        }

        public static int CompareNetworkIdentitySiblingPaths(NetworkIdentity left, NetworkIdentity right)
        {
            return CompareSiblingPaths(SiblingPathFor(left.transform), SiblingPathFor(right.transform));
        }

        [PostProcessScene]
        public static void OnPostProcessScene()
        {
            // vis2k: MISMATCHING SCENEID BUG FIX
            // problem:
            //   * FindObjectsOfType order is not guaranteed. restarting the
            //     editor results in a different order
            //   * connecting to a build again would cause UNET to deserialize
            //     the wrong objects, causing all kinds of weird errors like
            //     'ReadString out of range'
            //
            // solution:
            //   sort by sibling-index path, e.g. [0,1,2] vs [1]
            //   this is the only deterministic way to sort a list of objects in
            //   the scene.
            //   -> it's the same result every single time, even after restarts
            //
            // note: there is a reason why we 'sort by' sibling path instead of
            //   using it as sceneId directly. networkmanager etc. use Dont-
            //   DestroyOnLoad, which changes the hierarchy:
            //
            //     World:
            //       NetworkManager
            //       Player
            //
            //     ..becomes..
            //
            //     World:
            //       Player
            //     DontDestroyOnLoad:
            //       NetworkManager
            //
            //   so the player's siblingindex would be decreased by one.
            //   -> this is a problem because when building, OnPostProcessScene
            //      is called before any dontdestroyonload happens, but when
            //      entering play mode, it's called after
            //   -> hence sceneids would differ by one
            //
            //   => but if we only SORT it, then it doesn't matter if one
            //      inbetween disappeared. as long as no NetworkIdentity used
            //      DontDestroyOnLoad.
            //
            // note: assigning a GUID in NetworkIdentity.OnValidate would be way
            //   cooler, but OnValidate isn't called for other unopened scenes
            //   when building or pressing play, so the bug would still happen
            //   there.
            //
            // note: this can still fail if DontDestroyOnLoad is called for a
            // NetworkIdentity - but no one should ever do that anyway.
            List<NetworkIdentity> identities = FindObjectsOfType<NetworkIdentity>().ToList();
            identities.Sort(CompareNetworkIdentitySiblingPaths);

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
                offsetPerScene = uint.MaxValue / (uint)SceneManager.sceneCountInBuildSettings;

                // make sure that there aren't more sceneIds than offsetPerScene
                // -> only if we have multiple scenes. otherwise offset is 0, in
                //    which case it doesn't matter.
                if (identities.Count >= offsetPerScene)
                {
                    Debug.LogWarning(">=" + offsetPerScene + " NetworkIdentities in scene. Additive scene loading will cause duplicate ids.");
                }
            }

            uint nextSceneId = 1;
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

                uint offset = (uint)identity.gameObject.scene.buildIndex * offsetPerScene;
                identity.ForceSceneId(offset + nextSceneId++);
                if (LogFilter.Debug) { Debug.Log("PostProcess sceneid assigned: name=" + identity.name + " scene=" + identity.gameObject.scene.name + " sceneid=" + identity.sceneId); }

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
