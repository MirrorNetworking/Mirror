using System;
using UnityEngine;

namespace Mirror
{
    public delegate void OnClientConnect();
    public delegate void OnClientData(ArraySegment<byte> data, int channel);
    public delegate void OnClientError(Exception exception);
    public delegate void OnClientDisconnected();

    public interface IClientTransport : ICommonTransport
    {
        event OnClientConnect onConnected;
        event OnClientData onDataReceived;
        event OnClientError onError;
        event OnClientDisconnected onDisconnected;

        bool IsConnected();
        void Connect(string address);
        void Connect(Uri uri);
        void Disconnect();
        bool Send(int channelId, ArraySegment<byte> segment);
    }

    /// <summary>
    /// wrapper for Transport base class so that old transports can be used with IClientTransport
    /// </summary>
    public sealed class ClientOldTransportWrapper : IClientTransport
    {
        public Transport inner;

        public ClientOldTransportWrapper(Transport inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            inner.OnClientConnected.AddListener(() => onConnected.Invoke());
            inner.OnClientDataReceived.AddListener((d, c) => onDataReceived.Invoke(d, c));
            inner.OnClientError.AddListener((e) => onError.Invoke(e));
            inner.OnClientDisconnected.AddListener(() => onDisconnected.Invoke());
        }

        public event OnClientConnect onConnected;
        public event OnClientData onDataReceived;
        public event OnClientError onError;
        public event OnClientDisconnected onDisconnected;

        public bool enabled { get => inner.enabled; set => inner.enabled = value; }

        public bool Available() => inner.Available();
        public void Connect(string address) => inner.ClientConnect(address);
        public void Connect(Uri uri) => inner.ClientConnect(uri);
        public void Disconnect() => inner.ClientDisconnect();
        public int GetMaxPacketSize(int channelId = 0) => inner.GetMaxPacketSize(channelId);
        public bool IsConnected() => inner.ClientConnected();
        public bool Send(int channelId, ArraySegment<byte> segment) => inner.ClientSend(channelId, segment);
        public void Shutdown() => inner.Shutdown();

        void ICommonTransport.CheckForEvents()
        {
            Debug.LogWarning("Not Supported: Old transports dont have a way to check for events on demand");
        }
    }
}
