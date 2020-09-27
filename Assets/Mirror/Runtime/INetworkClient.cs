using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Mirror
{
    public interface IClientObjectManager
    {
        bool GetPrefab(Guid assetId, out GameObject prefab);

        void RegisterPrefab(GameObject prefab);

        void RegisterPrefab(GameObject prefab, Guid newAssetId);

        void RegisterPrefab(GameObject prefab, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler);

        void RegisterPrefab(GameObject prefab, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler);

        void UnregisterPrefab(GameObject prefab);

        void RegisterSpawnHandler(Guid assetId, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler);

        void RegisterSpawnHandler(Guid assetId, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler);

        void UnregisterSpawnHandler(Guid assetId);

        void ClearSpawners();

        void DestroyAllClientObjects();

        void PrepareToSpawnSceneObjects();
    }

    public interface INetworkClient
    {
        void Disconnect();

        void Send<T>(T message, int channelId = Channels.DefaultReliable);

        Task SendAsync<T>(T message, int channelId = Channels.DefaultReliable);
    }
}
