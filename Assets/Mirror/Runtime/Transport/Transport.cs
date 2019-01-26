// abstract transport layer component
// note: not all transports need a port, so add it to yours if needed.
using System;
using UnityEngine;

namespace Mirror
{
    // note: 'address' is ip / websocket url / ...
    public enum TransportEvent { Connected, Data, Disconnected }

    public abstract class Transport : MonoBehaviour
    {
        // client
        public abstract event Action ClientConnected;
        public abstract event Action<byte[]> ClientDataReceived;
        public abstract event Action<Exception> ClientErrored;
        public abstract event Action ClientDisconnected;

        public abstract bool IsClientConnected();
        public abstract void ClientConnect(string address);
        public abstract bool ClientSend(int channelId, byte[] data);
        public abstract void ClientDisconnect();

        // server
        public abstract event Action<int> ServerConnected;
        public abstract event Action<int, byte[]> ServerDataReceived;
        public abstract event Action<int, Exception> ServerErrored;
        public abstract event Action<int> ServerDisconnected;

        public abstract bool IsServerActive();
        public abstract void ServerStart();
        public abstract bool ServerSend(int connectionId, int channelId, byte[] data);
        public abstract bool ServerDisconnect(int connectionId);
        public abstract bool GetConnectionInfo(int connectionId, out string address);
        public abstract void ServerStop();

        // common
        public abstract void Shutdown();
        public abstract int GetMaxPacketSize(int channelId=Channels.DefaultReliable);
    }
}