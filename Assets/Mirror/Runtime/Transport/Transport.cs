// abstract transport layer component
// note: not all transports need a port, so add it to yours if needed.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Mirror
{
    // UnityEvent definitions
    [Serializable] public class ClientDataReceivedEvent : UnityEvent<ArraySegment<byte>, int> { }
    [Serializable] public class UnityEventException : UnityEvent<Exception> { }
    [Serializable] public class UnityEventInt : UnityEvent<int> { }
    [Serializable] public class ServerDataReceivedEvent : UnityEvent<int, ArraySegment<byte>, int> { }
    [Serializable] public class UnityEventIntException : UnityEvent<int, Exception> { }

    public abstract class Transport : MonoBehaviour
    {
        /// <summary>
        /// The current transport used by Mirror.
        /// </summary>
        public static Transport activeTransport;

        /// <summary>
        /// Is this transport available in the current platform?
        /// <para>Some transports might only be available in mobile</para>
        /// <para>Many will not work in webgl</para>
        /// <para>Example usage: return Application.platform == RuntimePlatform.WebGLPlayer</para>
        /// </summary>
        /// <returns>True if this transport works in the current platform</returns>
        public abstract bool Available();

        #region Client
        /// <summary>
        /// Notify subscribers when when this client establish a successful connection to the server
        /// </summary>
        [HideInInspector] public UnityEvent OnClientConnected = new UnityEvent();

        /// <summary>
        /// Notify subscribers when this client receive data from the server
        /// </summary>
        [HideInInspector] public ClientDataReceivedEvent OnClientDataReceived = new ClientDataReceivedEvent();

        /// <summary>
        /// Notify subscribers when this clianet encounters an error communicating with the server
        /// </summary>
        [HideInInspector] public UnityEventException OnClientError = new UnityEventException();

        /// <summary>
        /// Notify subscribers when this client disconnects from the server
        /// </summary>
        [HideInInspector] public UnityEvent OnClientDisconnected = new UnityEvent();

        /// <summary>
        /// Determines if we are currently connected to the server
        /// </summary>
        /// <returns>True if a connection has been established to the server</returns>
        public abstract bool ClientConnected();

        /// <summary>
        /// Establish a connection to a server
        /// </summary>
        /// <param name="address">The IP address or FQDN of the server we are trying to connect to</param>
        public abstract void ClientConnect(string address);

        /// <summary>
        /// Establish a connection to a server
        /// </summary>
        /// <param name="uri">The address of the server we are trying to connect to</param>
        public virtual void ClientConnect(Uri uri)
        {
            // By default, to keep backwards compatibility, just connect to the host
            // in the uri
            ClientConnect(uri.Host);
        }

        /// <summary>
        /// Send data to the server
        /// </summary>
        /// <param name="channelId">The channel to use.  0 is the default channel,
        /// but some transports might want to provide unreliable, encrypted, compressed, or any other feature
        /// as new channels</param>
        /// <param name="segment">The data to send to the server. Will be recycled after returning, so either use it directly or copy it internally. This allows for allocation-free sends!</param>
        /// <returns>true if the send was successful</returns>
        public abstract bool ClientSend(int channelId, ArraySegment<byte> segment);

        /// <summary>
        /// Disconnect this client from the server
        /// </summary>
        public abstract void ClientDisconnect();

        #endregion

        #region Server


        /// <summary>
        /// Retrieves the address of this server.
        /// Useful for network discovery
        /// </summary>
        /// <returns>the url at which this server can be reached</returns>
        public abstract Uri ServerUri();

        /// <summary>
        /// Notify subscribers when a client connects to this server
        /// </summary>
        [HideInInspector] public UnityEventInt OnServerConnected = new UnityEventInt();

        /// <summary>
        /// Notify subscribers when this server receives data from the client
        /// </summary>
        [HideInInspector] public ServerDataReceivedEvent OnServerDataReceived = new ServerDataReceivedEvent();

        /// <summary>
        /// Notify subscribers when this server has some problem communicating with the client
        /// </summary>
        [HideInInspector] public UnityEventIntException OnServerError = new UnityEventIntException();

        /// <summary>
        /// Notify subscribers when a client disconnects from this server
        /// </summary>
        [HideInInspector] public UnityEventInt OnServerDisconnected = new UnityEventInt();

        /// <summary>
        /// Determines if the server is up and running
        /// </summary>
        /// <returns>true if the transport is ready for connections from clients</returns>
        public abstract bool ServerActive();

        /// <summary>
        /// Start listening for clients
        /// </summary>
        public abstract void ServerStart();

        /// <summary>
        /// Send data to one or multiple clients. We provide a list, so that transports can make use
        /// of multicasting, and avoid allocations where possible.
        ///
        /// We don't provide a single ServerSend function to reduce complexity. Simply overwrite this
        /// one in your Transport.
        /// </summary>
        /// <param name="connectionIds">The list of client connection ids to send the data to</param>
        /// <param name="channelId">The channel to be used.  Transports can use channels to implement
        /// other features such as unreliable, encryption, compression, etc...</param>
        /// <param name="data"></param>
        /// <returns>true if the data was sent to all clients</returns>
        public abstract bool ServerSend(List<int> connectionIds, int channelId, ArraySegment<byte> segment);

        /// <summary>
        /// Disconnect a client from this server.  Useful to kick people out.
        /// </summary>
        /// <param name="connectionId">the id of the client to disconnect</param>
        /// <returns>true if the client was kicked</returns>
        public abstract bool ServerDisconnect(int connectionId);

        /// <summary>
        /// Get the client address
        /// </summary>
        /// <param name="connectionId">id of the client</param>
        /// <returns>address of the client</returns>
        public abstract string ServerGetClientAddress(int connectionId);

        /// <summary>
        /// Stop listening for clients and disconnect all existing clients
        /// </summary>
        public abstract void ServerStop();

        #endregion

        /// <summary>
        /// The maximum packet size for a given channel.  Unreliable transports
        /// usually can only deliver small packets. Reliable fragmented channels
        /// can usually deliver large ones.
        ///
        /// GetMaxPacketSize needs to return a value at all times. Even if the
        /// Transport isn't running, or isn't Available(). This is because
        /// Fallback and Multiplex transports need to find the smallest possible
        /// packet size at runtime.
        /// </summary>
        /// <param name="channelId">channel id</param>
        /// <returns>the size in bytes that can be sent via the provided channel</returns>
        public abstract int GetMaxPacketSize(int channelId = Channels.DefaultReliable);

        /// <summary>
        /// Shut down the transport, both as client and server
        /// </summary>
        public abstract void Shutdown();

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
        public void Update() { }

        /// <summary>
        /// called when quitting the application by closing the window / pressing stop in the editor
        /// <para>virtual so that inheriting classes' OnApplicationQuit() can call base.OnApplicationQuit() too</para>
        /// </summary>
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
