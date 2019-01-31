// abstract transport layer component
// note: not all transports need a port, so add it to yours if needed.
using System;
using UnityEngine;
using UnityEngine.Events;

namespace Mirror
{
    // UnityEvent definitions
    [Serializable] public class UnityEventByteArray : UnityEvent<byte[]> {}
    [Serializable] public class UnityEventException : UnityEvent<Exception> {}
    [Serializable] public class UnityEventInt : UnityEvent<int> {}
    [Serializable] public class UnityEventIntByteArray : UnityEvent<int, byte[]> {}
    [Serializable] public class UnityEventIntException : UnityEvent<int, Exception> {}

    public abstract class Transport : MonoBehaviour
    {
        // client
        [HideInInspector] public UnityEvent OnClientConnected;
        [HideInInspector] public UnityEventByteArray OnClientDataReceived;
        [HideInInspector] public UnityEventException OnClientError;
        [HideInInspector] public UnityEvent OnClientDisconnected;

        public abstract bool ClientConnected();
        public abstract void ClientConnect(string address);
        public abstract bool ClientSend(int channelId, byte[] data);
        public abstract void ClientDisconnect();

        // server
        [HideInInspector] public UnityEventInt OnServerConnected;
        [HideInInspector] public UnityEventIntByteArray OnServerDataReceived;
        [HideInInspector] public UnityEventIntException OnServerError;
        [HideInInspector] public UnityEventInt OnServerDisconnected;

        public abstract bool ServerActive();
        public abstract void ServerStart();
        public abstract bool ServerSend(int connectionId, int channelId, byte[] data);
        public abstract bool ServerDisconnect(int connectionId);
        public abstract bool GetConnectionInfo(int connectionId, out string address);
        public abstract void ServerStop();

        // common
        public abstract void Shutdown();
        public abstract int GetMaxPacketSize(int channelId=Channels.DefaultReliable);

        // block Update() to force Transports to use LateUpdate to avoid race
        // conditions. messages should be processed after all the game state
        // was processed in Update.
        // -> in other words: use LateUpdate!
        // -> uMMORPG 480 CCU stress test: when bot machine stops, it causes
        //    'Observer not ready for ...' log messages when using Update
        // -> occupying a public Update() function will cause Warnings if a
        //    transport uses Update.
        public void Update() {}
    }
}