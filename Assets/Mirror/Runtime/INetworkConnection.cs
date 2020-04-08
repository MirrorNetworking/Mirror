using System;
using System.Threading.Tasks;

namespace Mirror
{
    public interface INetworkConnection
    {
        void Disconnect();

        void RegisterHandler<T>(Action<NetworkConnection, T> handler, bool requireAuthentication = true)
                where T : IMessageBase, new();

        void RegisterHandler<T>(Action<T> handler, bool requireAuthentication = true) where T : IMessageBase, new();

        void UnregisterHandler<T>() where T : IMessageBase;

        void ClearHandlers();

        void Send<T>(T msg, int channelId = Channels.DefaultReliable) where T : IMessageBase;

        Task SendAsync<T>(T msg, int channelId = Channels.DefaultReliable) where T : IMessageBase;

        string ToString();
    }
}
