#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mirror
{
    public class NetworkIdentityManager
    {
        private static NetworkIdentityManager instance;
        private static Dictionary<uint, NetworkIdentity> lookup = new Dictionary<uint, NetworkIdentity>();
        private static List<NetworkIdentity> dupes = new List<NetworkIdentity>();
        private static bool sceneOpened;

        public static NetworkIdentityManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new NetworkIdentityManager();
                    instance.Setup();
                }

                return instance;
            }
        }

        private void Setup()
        {
            if (LogFilter.Debug)
                Debug.Log("[NetworkIdentityManager] Setup");
            EditorSceneManager.sceneOpening += this.OnSceneLoaded;
            EditorSceneManager.sceneOpened += this.OnSceneOpened;
            lookup.Clear();
            dupes.Clear();
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (LogFilter.Debug)
                Debug.LogFormat("[NetworkIdentityManager] OnSceneOpened {0}", scene.path);
            this.ProcessDupes();
            sceneOpened = true;
        }

        private void ProcessDupes()
        {
            foreach (NetworkIdentity ni in dupes)
            {
                if (LogFilter.Debug)
                    Debug.LogFormat("[NetworkIdentityManager] Forcing new sceneId on {0}, {1}", ni.sceneId, ni.gameObject.GetHierarchyPath());
                ni.FindAvailableSceneId(true);
                this.Add(ni);
            }
            dupes.Clear();
        }

        private void OnSceneLoaded(string path, OpenSceneMode mode)
        {
            sceneOpened = false;
            if (LogFilter.Debug)
                Debug.LogFormat("[NetworkIdentityManager] OnSceneLoaded {0}", path);
            this.Clear();
        }

        public bool DoesSceneIdExists(uint sceneId)
        {
            return lookup.ContainsKey(sceneId);
        }

        public void Add(NetworkIdentity networkIdentity)
        {
            if (lookup.ContainsKey(networkIdentity.sceneId))
            {
                // Dupe
                if (LogFilter.Debug)
                    Debug.LogFormat("[NetworkIdentityManager] Needs Refreshed({0}, {1})", networkIdentity.sceneId, networkIdentity.gameObject.GetHierarchyPath());

                dupes.Add(networkIdentity);

                if (sceneOpened)
                {
                    this.ProcessDupes();
                }
            }
            else
            {
                if (LogFilter.Debug)
                    Debug.LogFormat("[NetworkIdentityManager] Add({0}, {1})", networkIdentity.sceneId, networkIdentity.gameObject.GetHierarchyPath());
                lookup.Add(networkIdentity.sceneId, networkIdentity);
            }
        }

        public void Clear()
        {
            lookup.Clear();
            dupes.Clear();
        }
    }
}

#endif
