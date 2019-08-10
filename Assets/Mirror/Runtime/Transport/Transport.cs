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
        /// <summary>
        /// The current transport used by Mirror. 
        /// </summary>
        public static Transport activeTransport;

        /// <summary>
        /// Is this transport available in the current platform?
        /// <para>Some transports might only be available in mobile</para>
        /// <para>Many will not work in webgl</para>
        /// </summary>
        /// <returns>True if this transport works in the current platform</returns>
        public virtual bool Available()
        {
            return Application.platform != RuntimePlatform.WebGLPlayer;
        }

        #region Client
        /// <summary>
        /// Notify subscribers when when this client establish a successful connection to the server
        /// </summary>
        [HideInInspector] public UnityEvent OnClientConnected = new UnityEvent();

        /// <summary>
        /// Notify subscribers when this client receive data from the server
        /// </summary>
        [HideInInspector] public UnityEventArraySegment OnClientDataReceived = new UnityEventArraySegment();

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
        /// Establish a connecion to a server
        /// </summary>
        /// <param name="address">The IP address or FQDN of the server we are trying to connect to</param>
        public abstract void ClientConnect(string address);

        /// <summary>
        /// Send data to the server
        /// </summary>
        /// <param name="channelId">The channel to use.  0 is the default channel,
        /// but some transports might want to provide unreliable, encrypted, compressed, or any other feature
        /// as new channels</param>
        /// <param name="data">The data to send to the server</param>
        /// <returns>true if the send was successful</returns>
        public abstract bool ClientSend(int channelId, byte[] data);

        /// <summary>
        /// Disconnect this client from the server
        /// </summary>
        public abstract void ClientDisconnect();

        #endregion

        #region Server

        /// <summary>
        /// Notify subscribers when a client connects to this server
        /// </summary>
        [HideInInspector] public UnityEventInt OnServerConnected = new UnityEventInt();

        /// <summary>
        /// Notify subscribers when this server receives data from the client
        /// </summary>
        [HideInInspector] public UnityEventIntArraySegment OnServerDataReceived = new UnityEventIntArraySegment();

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
        /// Send data to a client
        /// </summary>
        /// <param name="connectionId">The id of the client to send the data to</param>
        /// <param name="channelId">The channel to be used.  Transports can use channels to implement
        /// other features such as unreliable, encryption, compression, etc...</param>
        /// <param name="data"></param>
        /// <returns>true if the data was sent</returns>
        public abstract bool ServerSend(int connectionId, int channelId, byte[] data);

        /// <summary>
        /// Disconnect a client from this server.  Useful to kick people out.
        /// </summary>
        /// <param name="connectionId">the id of the client to disconnect</param>
        /// <returns>true if the client was kicked</returns>
        public abstract bool ServerDisconnect(int connectionId);

        /// <summary>
        /// Deprecated: Use ServerGetClientAddress(int connectionId) instead
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use ServerGetClientAddress(int connectionId) instead")]
        public virtual bool GetConnectionInfo(int connectionId, out string address)
        {
            address = ServerGetClientAddress(connectionId);
            return true;
        }

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
        /// Shut down the transport, both as client and server
        /// </summary>
        public abstract void Shutdown();

        /// <summary>
        /// The maximum packet size for a given channel.  Unreliable transports
        /// usually can only deliver small packets.  Reliable fragmented channels
        /// can usually deliver large ones.
        /// </summary>
        /// <param name="channelId">channel id</param>
        /// <returns>the size in bytes that can be sent via the provided channel</returns>
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
