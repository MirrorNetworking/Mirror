using System;
using System.Net;
using Cysharp.Threading.Tasks;

namespace Mirror
{
    /// <summary>
    /// An object that can send and receive messages
    /// </summary>
    public interface IMessageHandler
    {
        void RegisterHandler<T>(Action<INetworkConnection, T> handler);

        void RegisterHandler<T>(Action<T> handler);

        void UnregisterHandler<T>();

        void ClearHandlers();

        void Send<T>(T msg, int channelId = Channel.Reliable);

        UniTask SendAsync<T>(T msg, int channelId = Channel.Reliable);

        UniTask SendAsync(ArraySegment<byte> segment, int channelId = Channel.Reliable);

        UniTask ProcessMessagesAsync();

    }

    /// <summary>
    /// An object that can observe NetworkIdentities.
    /// this is useful for interest management
    /// </summary>
    public interface IVisibilityTracker
    {
        void AddToVisList(NetworkIdentity identity);
        void RemoveFromVisList(NetworkIdentity identity);
        void RemoveObservers();
    }

    /// <summary>
    /// An object that can own networked objects
    /// </summary>
    public interface IObjectOwner
    {
        NetworkIdentity Identity { get; set; }
        void RemoveOwnedObject(NetworkIdentity networkIdentity);
        void AddOwnedObject(NetworkIdentity networkIdentity);
        void DestroyOwnedObjects();
    }

    /// <summary>
    /// A connection to a remote endpoint.
    /// May be from the server to client or from client to server
    /// </summary>
    public interface INetworkConnection : IMessageHandler, IVisibilityTracker, IObjectOwner
    {
        bool IsReady { get; set; }
        EndPoint Address { get; }
        object AuthenticationData { get; set; }

        void Disconnect();
    }
}
