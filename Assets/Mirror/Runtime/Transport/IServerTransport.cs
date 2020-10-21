using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public delegate void OnServerConnect(int connId);
    public delegate void OnServerData(int connId, ArraySegment<byte> data, int channel);
    //todo make connId nullable for connid
    public delegate void OnServerError(int connId, Exception exception);
    public delegate void OnServerDisconnected(int connId);

    public interface IServerTransport : ICommonTransport
    {
        event OnServerConnect onConnected;
        event OnServerData onDataReceived;
        event OnServerError onError;
        event OnServerDisconnected onDisconnected;

        bool IsActive();
        void Start();
        void Stop();
        bool Kick(int connectionId);
        bool Send(List<int> connectionIds, int channelId, ArraySegment<byte> segment);
        string GetClientAddress(int connectionId);
        Uri Uri();
    }

    /// <summary>
    /// wrapper for Transport base class so that old transports can be used with IServerTransport
    /// </summary>
    public sealed class ServerOldTransportWrapper : IServerTransport
    {
        public Transport inner;

        public ServerOldTransportWrapper(Transport inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            inner.OnServerConnected.AddListener((i) => onConnected.Invoke(i));
            inner.OnServerDataReceived.AddListener((i, d, c) => onDataReceived.Invoke(i, d, c));
            inner.OnServerError.AddListener((i, e) => onError.Invoke(i, e));
            inner.OnServerDisconnected.AddListener((i) => onDisconnected.Invoke(i));
        }

        public event OnServerConnect onConnected;
        public event OnServerData onDataReceived;
        public event OnServerError onError;
        public event OnServerDisconnected onDisconnected;

        public bool enabled { get => inner.enabled; set => inner.enabled = value; }

        public bool Available() => inner.Available();
        public string GetClientAddress(int connectionId) => inner.ServerGetClientAddress(connectionId);
        public int GetMaxPacketSize(int channelId = 0) => inner.GetMaxPacketSize(channelId);
        public bool IsActive() => inner.ServerActive();
        public bool Kick(int connectionId) => inner.ServerDisconnect(connectionId);
        public bool Send(List<int> connectionIds, int channelId, ArraySegment<byte> segment) => inner.ServerSend(connectionIds, channelId, segment);
        public void Shutdown() => inner.Shutdown();
        public void Start() => inner.ServerStart();
        public void Stop() => inner.ServerStop();
        public Uri Uri() => inner.ServerUri();

        void ICommonTransport.CheckForEvents()
        {
            Debug.LogWarning("Not Supported: Old transports dont have a way to check for events on demand");
        }
    }
}
