// Transport Rules
//
// All transports should follow these rules so that they work correctly with mirror:
// * When Monobehaviour is disabled the Transport should not invoke callbacks
// * Callbacks should be invoked on main thread. It is best to do this from LateUpdate
// * Callbacks can be invoked after ServerStop or ClientDisconnect has been called
// * ServerStop or ClientDisconnect can be called by mirror multiple times
// * Available should check the platform and 32 vs 64 bit if the transport only works on some of them
// * GetMaxPacketSize should return size even if transport is not running
// * Default channel should be reliable Channels.DefaultReliable
using System;
using UnityEngine;

namespace Mirror
{
    /// <summary>Abstract transport layer component</summary>
    public abstract class Transport : MonoBehaviour
    {
        /// <summary>The current transport used by Mirror.</summary>
        public static Transport activeTransport;

        /// <summary>Transport available on this platform? Some aren't available on all platforms.</summary>
        public abstract bool Available();

        /// <summary>Notify subscribers when this client establish a successful connection to the server</summary>
        public Action OnClientConnected = () => Debug.LogWarning("OnClientConnected called with no handler");

        /// <summary>Notify subscribers when this client receive data from the server</summary>
        public Action<ArraySegment<byte>, int> OnClientDataReceived = (data, channel) => Debug.LogWarning("OnClientDataReceived called with no handler");

        /// <summary>Notify subscribers when this client encounters an error communicating with the server</summary>
        public Action<Exception> OnClientError = (error) => Debug.LogWarning("OnClientError called with no handler");

        /// <summary>Notify subscribers when this client disconnects from the server</summary>
        public Action OnClientDisconnected = () => Debug.LogWarning("OnClientDisconnected called with no handler");

        /// <summary>Determines if we are currently connected to the server</summary>
        public abstract bool ClientConnected();

        /// <summary>Establish a connection to a server</summary>
        public abstract void ClientConnect(string address);

        /// <summary>Establish a connection to a server</summary>
        public virtual void ClientConnect(Uri uri)
        {
            // By default, to keep backwards compatibility, just connect to the host
            // in the uri
            ClientConnect(uri.Host);
        }

        /// <summary>Send data to the server over a given channel</summary>
        public abstract void ClientSend(int channelId, ArraySegment<byte> segment);

        /// <summary>Disconnect this client from the server</summary>
        public abstract void ClientDisconnect();

        /// <summary>Get the address of this server. Useful for network discovery</summary>
        public abstract Uri ServerUri();

        /// <summary>Notify subscribers when a client connects to this server</summary>
        public Action<int> OnServerConnected = (connId) => Debug.LogWarning("OnServerConnected called with no handler");

        /// <summary>Notify subscribers when this server receives data from the client</summary>
        public Action<int, ArraySegment<byte>, int> OnServerDataReceived = (connId, data, channel) => Debug.LogWarning("OnServerDataReceived called with no handler");

        /// <summary>Notify subscribers when this server has some problem communicating with the client</summary>
        public Action<int, Exception> OnServerError = (connId, error) => Debug.LogWarning("OnServerError called with no handler");

        /// <summary>Notify subscribers when a client disconnects from this server</summary>
        public Action<int> OnServerDisconnected = (connId) => Debug.LogWarning("OnServerDisconnected called with no handler");

        /// <summary>Determines if the server is up and running</summary>
        public abstract bool ServerActive();

        /// <summary>Start listening for clients</summary>
        public abstract void ServerStart();

        /// <summary>Send data to the client with connectionId over the given channel.</summary>
        public abstract void ServerSend(int connectionId, int channelId, ArraySegment<byte> segment);

        /// <summary>Disconnect a client from this server. Useful to kick people out.</summary>
        public abstract bool ServerDisconnect(int connectionId);

        /// <summary>Get the client address, useful for IP bans etc.</summary>
        public abstract string ServerGetClientAddress(int connectionId);

        /// <summary>Stop listening for clients and disconnect all existing clients</summary>
        public abstract void ServerStop();

        /// <summary>The maximum packet size for a given channel.</summary>
        // Unreliable transports usually can only deliver small packets.
        // Reliable fragmented channels can usually deliver large ones.
        //
        // GetMaxPacketSize needs to return a value at all times. Even if the
        // Transport isn't running, or isn't Available(). This is because
        // Fallback and Multiplex transports need to find the smallest possible
        // packet size at runtime.
        public abstract int GetMaxPacketSize(int channelId = Channels.DefaultReliable);

        /// <summary>The maximum batch(!) size for a given channel.</summary>
        // Uses GetMaxPacketSize by default.
        // Some transports like kcp support large max packet sizes which should
        // not be used for batching all the time because they end up being too
        // slow (head of line blocking etc.).
        public virtual int GetMaxBatchSize(int channelId) =>
            GetMaxPacketSize(channelId);

        // block Update & LateUpdate to show warnings if Transports still use
        // them instead of using
        //   Client/ServerEarlyUpdate: to process incoming messages
        //   Client/ServerLateUpdate: to process outgoing messages
        // those are called by NetworkClient/Server at the right time.
        //
        // allows transports to implement the proper network update order of:
        //      process_incoming()
        //      update_world()
        //      process_outgoing()
        //
        // => see NetworkLoop.cs for detailed explanations!
#pragma warning disable UNT0001 // Empty Unity message
        public void Update() {}
        public void LateUpdate() {}
#pragma warning restore UNT0001 // Empty Unity message

        // NetworkLoop NetworkEarly/LateUpdate were added for a proper network
        // update order. the goal is to:
        //    process_incoming()
        //    update_world()
        //    process_outgoing()
        // in order to avoid unnecessary latency and data races.
        // => split into client and server parts so that we can cleanly call
        //    them from NetworkClient/Server
        // => VIRTUAL for now so we can take our time to convert transports
        //    without breaking anything.
        public virtual void ClientEarlyUpdate() {}
        public virtual void ServerEarlyUpdate() {}
        public virtual void ClientLateUpdate() {}
        public virtual void ServerLateUpdate() {}

        /// <summary>Shut down the transport, both as client and server</summary>
        public abstract void Shutdown();

        // called when quitting the application by closing the window / pressing
        // stop in the editor.
        // virtual so that inheriting classes' OnApplicationQuit() can call
        // base.OnApplicationQuit() too</para>
        public virtual void OnApplicationQuit()
        {
            // stop transport (e.g. to shut down threads)
            // (when pressing Stop in the Editor, Unity keeps threads alive
            //  until we press Start again. so if Transports use threads, we
            //  really want them to end now and not after next start)
            Shutdown();
        }
    }
}
