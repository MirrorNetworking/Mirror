using System.Text;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

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
            var prefabWarnings = new HashSet<string>();

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
            var uvs = FindObjectsOfType<NetworkIdentity>().ToList();
            uvs.Sort(CompareNetworkIdentitySiblingPaths);

            int nextSceneId = 1;
            foreach (NetworkIdentity uv in uvs)
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
                if (LogFilter.logDebug) { Debug.Log("PostProcess sceneid assigned: name=" + uv.name + " scene=" + uv.gameObject.scene.name + " sceneid=" + uv.sceneId); }

                // saftey check for prefabs with more than one NetworkIdentity
                var prefabGO = PrefabUtility.GetPrefabParent(uv.gameObject) as GameObject;
                if (prefabGO)
                {
                    var prefabRootGO = PrefabUtility.FindPrefabRoot(prefabGO);
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
