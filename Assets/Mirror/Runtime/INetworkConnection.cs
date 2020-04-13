using System;
using System.Net;
using System.Threading.Tasks;

namespace Mirror
{
    /// <summary>
    /// An object that can send and receive messages
    /// </summary>
    public interface IMessageHandler
    {
        void RegisterHandler<T>(Action<INetworkConnection, T> handler)
                where T : IMessageBase, new();

        void RegisterHandler<T>(Action<T> handler) where T : IMessageBase, new();

        void UnregisterHandler<T>() where T : IMessageBase;

        void ClearHandlers();

        void Send<T>(T msg, int channelId = Channels.DefaultReliable) where T : IMessageBase;

        Task SendAsync<T>(T msg, int channelId = Channels.DefaultReliable) where T : IMessageBase;

        Task ProcessMessagesAsync();

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
