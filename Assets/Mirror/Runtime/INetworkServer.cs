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

        void Spawn(GameObject obj, GameObject player);

        void Spawn(GameObject obj, INetworkConnection ownerConnection = null);

        void Spawn(GameObject obj, Guid assetId, INetworkConnection ownerConnection = null);

        void Destroy(GameObject obj);

        void UnSpawn(GameObject obj);

        bool SpawnObjects();
    }

    public interface INetworkServer : IServerObjectManager
    {
        void Disconnect();

        void AddConnection(INetworkConnection conn);

        void RemoveConnection(INetworkConnection conn);

        void SendToAll<T>(T msg, int channelId = Channels.DefaultReliable) where T : IMessageBase;

        void SendToReady<T>(NetworkIdentity identity, T msg, int channelId) where T : IMessageBase;

        void SendToReady<T>(NetworkIdentity identity, T msg, bool includeOwner = true, int channelId = Channels.DefaultReliable) where T : IMessageBase;

        void SendToClientOfPlayer<T>(NetworkIdentity identity, T msg, int channelId = Channels.DefaultReliable) where T : IMessageBase;

        void SetClientReady(INetworkConnection conn);

        void SetAllClientsNotReady();

        void SetClientNotReady(INetworkConnection conn);
    }
}
