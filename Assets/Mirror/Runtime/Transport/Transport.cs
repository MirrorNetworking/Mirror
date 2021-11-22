// For future reference, here is what Transports need to do in Mirror:
//
// Connecting:
//   * Transports are responsible to call either OnConnected || OnDisconnected
//     in a certain time after a Connect was called. It can not end in limbo.
//
// Disconnecting:
//   * Connections might disconnect voluntarily by the other end.
//   * Connections might be disconnect involuntarily by the server.
//   * Either way, Transports need to detect it and call OnDisconnected.
//
// Timeouts:
//   * Transports should expose a configurable timeout
//   * Transports are responsible for calling OnDisconnected after a timeout
//
// Channels:
//   * Default channel is Reliable, as in reliable ordered (OR DISCONNECT)
//   * Where possible, Unreliable should be supported (unordered, no guarantee)
//
// Other:
//   * Transports functions are all bound to the main thread.
//     (Transports can use other threads in the background if they manage them)
//   * Transports should only process messages while the component is enabled.
//
using System;
using UnityEngine;

namespace Mirror
{
    /// <summary>Abstract transport layer component</summary>
    public abstract class Transport : MonoBehaviour
    {
        /// <summary>The current transport used by Mirror.</summary>
        public static Transport activeTransport;

        /// <summary>Is this transport available in the current platform?</summary>
        public abstract bool Available();

        /// <summary>Called by Transport when the client connected to the server.</summary>
        public Action OnClientConnected = () => Debug.LogWarning("OnClientConnected called with no handler");

        /// <summary>Called by Transport when the client received a message from the server.</summary>
        public Action<ArraySegment<byte>, int> OnClientDataReceived = (data, channel) => Debug.LogWarning("OnClientDataReceived called with no handler");

        /// <summary>Called by Transport when the client encountered an error.</summary>
        public Action<Exception> OnClientError = (error) => Debug.LogWarning("OnClientError called with no handler");

        /// <summary>Called by Transport when the client disconnected from the server.</summary>
        public Action OnClientDisconnected = () => Debug.LogWarning("OnClientDisconnected called with no handler");

        /// <summary>True if the client is currently connected to the server.</summary>
        public abstract bool ClientConnected();

        /// <summary>Connects the client to the server at the address.</summary>
        public abstract void ClientConnect(string address);

        /// <summary>Connects the client to the server at the Uri.</summary>
        public virtual void ClientConnect(Uri uri)
        {
            // By default, to keep backwards compatibility, just connect to the host
            // in the uri
            ClientConnect(uri.Host);
        }

        /// <summary>Sends a message to the server over the given channel.</summary>
        // The ArraySegment is only valid until returning. Copy if needed.
        public abstract void ClientSend(ArraySegment<byte> segment, int channelId);

        /// <summary>Disconnects the client from the server</summary>
        public abstract void ClientDisconnect();

        /// <summary>Returns server address as Uri.</summary>
        // Useful for NetworkDiscovery.
        public abstract Uri ServerUri();

        /// <summary>Called by Transport when a new client connected to the server.</summary>
        public Action<int> OnServerConnected = (connId) => Debug.LogWarning("OnServerConnected called with no handler");

        /// <summary>Called by Transport when the server received a message from a client.</summary>
        public Action<int, ArraySegment<byte>, int> OnServerDataReceived = (connId, data, channel) => Debug.LogWarning("OnServerDataReceived called with no handler");

        /// <summary>Called by Transport when a server's connection encountered a problem.</summary>
        /// If a Disconnect will also be raised, raise the Error first.
        public Action<int, Exception> OnServerError = (connId, error) => Debug.LogWarning("OnServerError called with no handler");

        /// <summary>Called by Transport when a client disconnected from the server.</summary>
        public Action<int> OnServerDisconnected = (connId) => Debug.LogWarning("OnServerDisconnected called with no handler");

        /// <summary>True if the server is currently listening for connections.</summary>
        public abstract bool ServerActive();

        /// <summary>Start listening for connections.</summary>
        public abstract void ServerStart();

        /// <summary>Send a message to a client over the given channel.</summary>
        public abstract void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId);

        /// <summary>Disconnect a client from the server.</summary>
        public abstract void ServerDisconnect(int connectionId);

        /// <summary>Get a client's address on the server.</summary>
        // Can be useful for Game Master IP bans etc.
        public abstract string ServerGetClientAddress(int connectionId);

        /// <summary>Stop listening and disconnect all connections.</summary>
        public abstract void ServerStop();

        /// <summary>Maximum message size for the given channel.</summary>
        // Different channels often have different sizes, ranging from MTU to
        // several megabytes.
        //
        // Needs to return a value at all times, even if the Transport isn't
        // running or available because it's needed for initializations.
        public abstract int GetMaxPacketSize(int channelId = Channels.Reliable);

        /// <summary>Recommended Batching threshold for this transport.</summary>
        // Uses GetMaxPacketSize by default.
        // Some transports like kcp support large max packet sizes which should
        // not be used for batching all the time because they end up being too
        // slow (head of line blocking etc.).
        public virtual int GetBatchThreshold(int channelId)
        {
            return GetMaxPacketSize(channelId);
        }

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

        /// <summary>
        /// NetworkLoop NetworkEarly/LateUpdate were added for a proper network
        /// update order. the goal is to:
        ///    process_incoming()
        ///    update_world()
        ///    process_outgoing()
        /// in order to avoid unnecessary latency and data races.
        /// </summary>
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

        /// <summary>Called by Unity when quitting. Inheriting Transports should call base for proper Shutdown.</summary>
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
