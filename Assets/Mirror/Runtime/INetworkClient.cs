using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Mirror
{
    public delegate GameObject SpawnHandlerDelegate(SpawnMessage msg);

    // Handles requests to unspawn objects on the client
    public delegate void UnSpawnDelegate(GameObject spawned);

    public interface IClientObjectManager
    {
        GameObject GetPrefab(Guid assetId);

        void RegisterPrefab(GameObject prefab);

        void RegisterPrefab(GameObject prefab, Guid newAssetId);

        void RegisterPrefab(GameObject prefab, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler);

        void UnregisterPrefab(GameObject prefab);

        void RegisterSpawnHandler(Guid assetId, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler);

        void UnregisterSpawnHandler(Guid assetId);

        void ClearSpawners();

        void DestroyAllClientObjects();

        void PrepareToSpawnSceneObjects();
    }

    public interface INetworkClient
    {
        void Disconnect();

        void Send<T>(T message, int channelId = Channel.Reliable);

        UniTask SendAsync<T>(T message, int channelId = Channel.Reliable);
    }
}
