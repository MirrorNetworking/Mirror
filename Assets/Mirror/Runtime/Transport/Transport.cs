// abstract transport layer component
// note: not all transports need a port, so add it to yours if needed.
using System;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Events;

namespace Mirror
{
    // UnityEvent definitions
    [Serializable] public class UnityEventArraySegment : UnityEvent<ArraySegment<byte>> {}
    [Serializable] public class UnityEventException : UnityEvent<Exception> {}
    [Serializable] public class UnityEventInt : UnityEvent<int> {}
    [Serializable] public class UnityEventIntArraySegment : UnityEvent<int, ArraySegment<byte>> {}
    [Serializable] public class UnityEventIntException : UnityEvent<int, Exception> {}

    public abstract class Transport : MonoBehaviour
    {
        // static Transport which receives all network events
        // this is usually set by NetworkManager, but doesn't have to be.
        public static Transport activeTransport;

        // determines if the transport is available for this platform
        // by default a transport is available in all platforms except webgl
        public virtual bool Available()
        {
            return Application.platform != RuntimePlatform.WebGLPlayer;
        }

        // client
        [HideInInspector] public UnityEvent OnClientConnected = new UnityEvent();
        [HideInInspector] public UnityEventArraySegment OnClientDataReceived = new UnityEventArraySegment();
        [HideInInspector] public UnityEventException OnClientError = new UnityEventException();
        [HideInInspector] public UnityEvent OnClientDisconnected = new UnityEvent();

        public abstract bool ClientConnected();
        public abstract void ClientConnect(string address);
        public abstract bool ClientSend(int channelId, byte[] data);
        public abstract void ClientDisconnect();

        // server
        [HideInInspector] public UnityEventInt OnServerConnected = new UnityEventInt();
        [HideInInspector] public UnityEventIntArraySegment OnServerDataReceived = new UnityEventIntArraySegment();
        [HideInInspector] public UnityEventIntException OnServerError = new UnityEventIntException();
        [HideInInspector] public UnityEventInt OnServerDisconnected = new UnityEventInt();

        public abstract bool ServerActive();
        public abstract void ServerStart();
        public abstract bool ServerSend(int connectionId, int channelId, byte[] data);
        public abstract bool ServerDisconnect(int connectionId);

        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use ServerGetClientAddress(int connectionId) instead")]
        public virtual bool GetConnectionInfo(int connectionId, out string address)
        {
            address = ServerGetClientAddress(connectionId);
            return true;
        }

        public abstract string ServerGetClientAddress(int connectionId);
        public abstract void ServerStop();

        // common
        public abstract void Shutdown();
        public abstract int GetMaxPacketSize(int channelId = Channels.DefaultReliable);

        // block Update() to force Transports to use LateUpdate to avoid race
        // conditions. messages should be processed after all the game state
        // was processed in Update.
        // -> in other words: use LateUpdate!
        // -> uMMORPG 480 CCU stress test: when bot machine stops, it causes
        //    'Observer not ready for ...' log messages when using Update
        // -> occupying a public Update() function will cause Warnings if a
        //    transport uses Update.
        //
        // IMPORTANT: set script execution order to >1000 to call Transport's
        //            LateUpdate after all others. Fixes race condition where
        //            e.g. in uSurvival Transport would apply Cmds before
        //            ShoulderRotation.LateUpdate, resulting in projectile
        //            spawns at the point before shoulder rotation.
        public void Update() {}
    }
}
