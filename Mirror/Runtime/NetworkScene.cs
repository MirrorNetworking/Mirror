using System.Collections.Generic;
using UnityEngine;
using Guid = System.Guid;

namespace Mirror
{
    // This is an internal class to allow the client and server to share scene-related functionality.
    // This code (mostly) used to be in ClientScene.
    internal class NetworkScene
    {
        internal static Dictionary<Guid, GameObject> guidToPrefab = new Dictionary<Guid, GameObject>();
        internal static Dictionary<Guid, SpawnDelegate> spawnHandlers = new Dictionary<Guid, SpawnDelegate>();
        internal static Dictionary<Guid, UnSpawnDelegate> unspawnHandlers = new Dictionary<Guid, UnSpawnDelegate>();

        internal void Shutdown()
        {
            NetworkIdentity.spawned.Clear();
            ClearSpawners();
        }

        internal void SetLocalObject(uint netId, GameObject obj, bool isClient, bool isServer)
        {
            if (LogFilter.Debug) { Debug.Log("SetLocalObject " + netId + " " + obj); }

            if (obj == null)
            {
                NetworkIdentity.spawned[netId] = null;
                return;
            }

            NetworkIdentity foundNetworkIdentity;
            NetworkIdentity.spawned.TryGetValue(netId, out foundNetworkIdentity);

            if (foundNetworkIdentity == null)
            {
                foundNetworkIdentity = obj.GetComponent<NetworkIdentity>();
                NetworkIdentity.spawned[netId] = foundNetworkIdentity;
            }

            foundNetworkIdentity.UpdateClientServer(isClient, isServer);
        }

        // this lets the client take an instance ID from the server and find
        // the local object that it corresponds too. This is temporary until
        // object references can be serialized transparently.
        internal GameObject FindLocalObject(uint netId)
        {
            NetworkIdentity identity;
            if (GetNetworkIdentity(netId, out identity))
            {
                return identity.gameObject;
            }
            return null;
        }

        internal bool GetNetworkIdentity(uint netId, out NetworkIdentity uv)
        {
            return NetworkIdentity.spawned.TryGetValue(netId, out uv) && uv != null;
        }

        internal static void RegisterPrefab(GameObject prefab, Guid newAssetId)
        {
            NetworkIdentity view = prefab.GetComponent<NetworkIdentity>();
            if (view)
            {
                view.SetDynamicAssetId(newAssetId);

                if (LogFilter.Debug) { Debug.Log("Registering prefab '" + prefab.name + "' as asset:" + view.assetId); }
                guidToPrefab[view.assetId] = prefab;
            }
            else
            {
                Debug.LogError("Could not register '" + prefab.name + "' since it contains no NetworkIdentity component");
            }
        }

        internal static void RegisterPrefab(GameObject prefab)
        {
            NetworkIdentity view = prefab.GetComponent<NetworkIdentity>();
            if (view)
            {
                if (LogFilter.Debug) { Debug.Log("Registering prefab '" + prefab.name + "' as asset:" + view.assetId); }
                guidToPrefab[view.assetId] = prefab;

                var uvs = prefab.GetComponentsInChildren<NetworkIdentity>();
                if (uvs.Length > 1)
                {
                    Debug.LogWarning("The prefab '" + prefab.name +
                        "' has multiple NetworkIdentity components. There can only be one NetworkIdentity on a prefab, and it must be on the root object.");
                }
            }
            else
            {
                Debug.LogError("Could not register '" + prefab.name + "' since it contains no NetworkIdentity component");
            }
        }

        internal static bool GetPrefab(Guid assetId, out GameObject prefab)
        {
            prefab = null;
            if (assetId != Guid.Empty && guidToPrefab.ContainsKey(assetId) && guidToPrefab[assetId] != null)
            {
                prefab = guidToPrefab[assetId];
                return true;
            }
            return false;
        }

        internal static void ClearSpawners()
        {
            guidToPrefab.Clear();
            spawnHandlers.Clear();
            unspawnHandlers.Clear();
        }

        public static void UnregisterSpawnHandler(Guid assetId)
        {
            spawnHandlers.Remove(assetId);
            unspawnHandlers.Remove(assetId);
        }

        internal static void RegisterSpawnHandler(Guid assetId, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            if (spawnHandler == null || unspawnHandler == null)
            {
                Debug.LogError("RegisterSpawnHandler custom spawn function null for " + assetId);
                return;
            }

            if (LogFilter.Debug) { Debug.Log("RegisterSpawnHandler asset '" + assetId + "' " + spawnHandler.GetMethodName() + "/" + unspawnHandler.GetMethodName()); }

            spawnHandlers[assetId] = spawnHandler;
            unspawnHandlers[assetId] = unspawnHandler;
        }

        internal static void UnregisterPrefab(GameObject prefab)
        {
            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError("Could not unregister '" + prefab.name + "' since it contains no NetworkIdentity component");
                return;
            }
            spawnHandlers.Remove(identity.assetId);
            unspawnHandlers.Remove(identity.assetId);
        }

        internal static void RegisterPrefab(GameObject prefab, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError("Could not register '" + prefab.name + "' since it contains no NetworkIdentity component");
                return;
            }

            if (spawnHandler == null || unspawnHandler == null)
            {
                Debug.LogError("RegisterPrefab custom spawn function null for " + identity.assetId);
                return;
            }

            if (identity.assetId == Guid.Empty)
            {
                Debug.LogError("RegisterPrefab game object " + prefab.name + " has no prefab. Use RegisterSpawnHandler() instead?");
                return;
            }

            if (LogFilter.Debug) { Debug.Log("Registering custom prefab '" + prefab.name + "' as asset:" + identity.assetId + " " + spawnHandler.GetMethodName() + "/" + unspawnHandler.GetMethodName()); }

            spawnHandlers[identity.assetId] = spawnHandler;
            unspawnHandlers[identity.assetId] = unspawnHandler;
        }

        internal static bool GetSpawnHandler(Guid assetId, out SpawnDelegate handler)
        {
            if (spawnHandlers.ContainsKey(assetId))
            {
                handler = spawnHandlers[assetId];
                return true;
            }
            handler = null;
            return false;
        }

        internal static bool InvokeUnSpawnHandler(Guid assetId, GameObject obj)
        {
            if (unspawnHandlers.ContainsKey(assetId) && unspawnHandlers[assetId] != null)
            {
                UnSpawnDelegate handler = unspawnHandlers[assetId];
                handler(obj);
                return true;
            }
            return false;
        }

        internal void DestroyAllClientObjects()
        {
            foreach (var netId in NetworkIdentity.spawned.Keys)
            {
                NetworkIdentity uv = NetworkIdentity.spawned[netId];

                if (uv != null && uv.gameObject != null)
                {
                    if (!InvokeUnSpawnHandler(uv.assetId, uv.gameObject))
                    {
                        if (uv.sceneId == 0)
                        {
                            Object.Destroy(uv.gameObject);
                        }
                        else
                        {
                            uv.MarkForReset();
                            uv.gameObject.SetActive(false);
                        }
                    }
                }
            }
            NetworkIdentity.spawned.Clear();
        }
    }
}
