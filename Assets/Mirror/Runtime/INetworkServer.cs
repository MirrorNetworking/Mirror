using System;
using UnityEngine;

namespace Mirror
{
    public interface IServerObjectManager
    {
        bool AddPlayerForConnection(INetworkConnection conn, GameObject player);

        bool AddPlayerForConnection(INetworkConnection conn, GameObject player, Guid assetId);

        bool ReplacePlayerForConnection(INetworkConnection conn, NetworkClient client, GameObject player, bool keepAuthority = false);

        bool ReplacePlayerForConnection(INetworkConnection conn, NetworkClient client, GameObject player, Guid assetId, bool keepAuthority = false);

        void Spawn(GameObject obj, GameObject ownerPlayer);

        void Spawn(GameObject obj, INetworkConnection ownerConnection = null);

        void Spawn(GameObject obj, Guid assetId, INetworkConnection ownerConnection = null);

        void Destroy(GameObject obj);

        void UnSpawn(GameObject obj);

        bool SpawnObjects();
    }

    public interface INetworkServer
    {
        void Disconnect();

        void AddConnection(INetworkConnection conn);

        void RemoveConnection(INetworkConnection conn);

        void SendToAll<T>(T msg, int channelId = Channel.Reliable);

        void SendToClientOfPlayer<T>(NetworkIdentity identity, T msg, int channelId = Channel.Reliable);
    }
}
