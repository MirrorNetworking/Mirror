// moved into NetworkClient on 2021-03-07
using System;
using System.Collections.Generic;
using UnityEngine;
using Guid = System.Guid;

namespace Mirror
{
    // Deprecated 2021-03-07
    [Obsolete("Use NetworkClient instead")]
    public static class ClientScene
    {
        [Obsolete("ClientScene.localPlayer was moved to NetworkClient.localPlayer")]
        public static NetworkIdentity localPlayer
        {
            get { return NetworkClient.localPlayer; }
            set { NetworkClient.localPlayer = value; }
        }

        [Obsolete("ClientScene.ready was moved to NetworkClient.ready")]
        public static bool ready
        {
            get { return NetworkClient.ready; }
            set { NetworkClient.ready = value; }
        }

        [Obsolete("ClientScene.readyConnection was moved to NetworkClient.readyConnection")]
        public static NetworkConnection readyConnection
        {
            get { return NetworkClient.readyConnection; }
            set { NetworkClient.connection = value; }
        }

        [Obsolete("ClientScene.prefabs was moved to NetworkClient.prefabs")]
        public static Dictionary<Guid, GameObject> prefabs => NetworkClient.prefabs;

        // add player //////////////////////////////////////////////////////////
        [Obsolete("ClientScene.AddPlayer was moved to NetworkClient.AddPlayer")]
        public static bool AddPlayer(NetworkConnection readyConn) => NetworkClient.AddPlayer(readyConn);

        // ready ///////////////////////////////////////////////////////////////
        [Obsolete("ClientScene.Ready was moved to NetworkClient.Ready")]
        public static bool Ready(NetworkConnection conn) => NetworkClient.Ready(conn);

        [Obsolete("ClientScene.PrepareToSpawnSceneObjects was moved to NetworkClient.PrepareToSpawnSceneObjects")]
        public static void PrepareToSpawnSceneObjects() => NetworkClient.PrepareToSpawnSceneObjects();

        // spawnable prefabs ///////////////////////////////////////////////////
        [Obsolete("ClientScene.GetPrefab was moved to NetworkClient.GetPrefab")]
        public static bool GetPrefab(Guid assetId, out GameObject prefab) => NetworkClient.GetPrefab(assetId, out prefab);

        [Obsolete("ClientScene.RegisterPrefab was moved to NetworkClient.RegisterPrefab")]
        public static void RegisterPrefab(GameObject prefab, Guid newAssetId) => NetworkClient.RegisterPrefab(prefab, newAssetId);

        [Obsolete("ClientScene.RegisterPrefab was moved to NetworkClient.RegisterPrefab")]
        public static void RegisterPrefab(GameObject prefab) => NetworkClient.RegisterPrefab(prefab);

        [Obsolete("ClientScene.RegisterPrefab was moved to NetworkClient.RegisterPrefab")]
        public static void RegisterPrefab(GameObject prefab, Guid newAssetId, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler) =>
            NetworkClient.RegisterPrefab(prefab, newAssetId, spawnHandler, unspawnHandler);

        [Obsolete("ClientScene.RegisterPrefab was moved to NetworkClient.RegisterPrefab")]
        public static void RegisterPrefab(GameObject prefab, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler) =>
            NetworkClient.RegisterPrefab(prefab, spawnHandler, unspawnHandler);

        [Obsolete("ClientScene.RegisterPrefab was moved to NetworkClient.RegisterPrefab")]
        public static void RegisterPrefab(GameObject prefab, Guid newAssetId, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler) =>
            NetworkClient.RegisterPrefab(prefab, newAssetId, spawnHandler, unspawnHandler);

        [Obsolete("ClientScene.RegisterPrefab was moved to NetworkClient.RegisterPrefab")]
        public static void RegisterPrefab(GameObject prefab, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler) =>
            NetworkClient.RegisterPrefab(prefab, spawnHandler, unspawnHandler);

        [Obsolete("ClientScene.UnregisterPrefab was moved to NetworkClient.UnregisterPrefab")]
        public static void UnregisterPrefab(GameObject prefab) => NetworkClient.UnregisterPrefab(prefab);

        // spawn handlers //////////////////////////////////////////////////////
        [Obsolete("ClientScene.RegisterSpawnHandler was moved to NetworkClient.RegisterSpawnHandler")]
        public static void RegisterSpawnHandler(Guid assetId, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler) =>
            NetworkClient.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);

        [Obsolete("ClientScene.RegisterSpawnHandler was moved to NetworkClient.RegisterSpawnHandler")]
        public static void RegisterSpawnHandler(Guid assetId, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler) =>
            NetworkClient.RegisterSpawnHandler(assetId, spawnHandler, unspawnHandler);

        [Obsolete("ClientScene.UnregisterSpawnHandler was moved to NetworkClient.UnregisterSpawnHandler")]
        public static void UnregisterSpawnHandler(Guid assetId) => NetworkClient.UnregisterSpawnHandler(assetId);

        [Obsolete("ClientScene.ClearSpawners was moved to NetworkClient.ClearSpawners")]
        public static void ClearSpawners() => NetworkClient.ClearSpawners();

        [Obsolete("ClientScene.DestroyAllClientObjects was moved to NetworkClient.DestroyAllClientObjects")]
        public static void DestroyAllClientObjects() => NetworkClient.DestroyAllClientObjects();
    }
}
