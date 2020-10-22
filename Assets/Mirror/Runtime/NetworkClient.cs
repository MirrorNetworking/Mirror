using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public enum ConnectState
    {
        None,
        Connecting,
        Connected,
        Disconnected
    }

    /// <summary>
    /// This is a network client class used by the networking system. It contains a NetworkConnection that is used to connect to a network server.
    /// <para>The <see cref="NetworkClient">NetworkClient</see> handle connection state, messages handlers, and connection configuration. There can be many <see cref="NetworkClient">NetworkClient</see> instances in a process at a time, but only one that is connected to a game server (<see cref="NetworkServer">NetworkServer</see>) that uses spawned objects.</para>
    /// <para><see cref="NetworkClient">NetworkClient</see> has an internal update function where it handles events from the transport layer. This includes asynchronous connect events, disconnect events and incoming data from a server.</para>
    /// <para>The <see cref="NetworkManager">NetworkManager</see> has a NetworkClient instance that it uses for games that it starts, but the NetworkClient may be used by itself.</para>
    /// </summary>
    public static class NetworkClient
    {
        /// <summary>
        /// The registered network message handlers.
        /// </summary>
        static readonly Dictionary<int, NetworkMessageDelegate> handlers = new Dictionary<int, NetworkMessageDelegate>();

        /// <summary>
        /// The NetworkConnection object this client is using.
        /// </summary>
        public static NetworkConnection connection { get; internal set; }

        internal static ConnectState connectState = ConnectState.None;

        /// <summary>
        /// The IP address of the server that this client is connected to.
        /// <para>This will be empty if the client has not connected yet.</para>
        /// </summary>
        public static string serverIp => connection.address;

        /// <summary>
        /// active is true while a client is connecting/connected
        /// (= while the network is active)
        /// </summary>
        public static bool active => connectState == ConnectState.Connecting || connectState == ConnectState.Connected;

        /// <summary>
        /// This gives the current connection status of the client.
        /// </summary>
        public static bool isConnected => connectState == ConnectState.Connected;

        /// <summary>
        /// Connect client to a NetworkServer instance.
        /// </summary>
        /// <param name="address"></param>
        public static void Connect(string address)
        {
            // Debug.Log("Client Connect: " + address);

            RegisterSystemHandlers();
            Transport.activeTransport.enabled = true;
            InitializeTransportHandlers();

            connectState = ConnectState.Connecting;
            Transport.activeTransport.ClientConnect(address);

            // setup all the handlers
            connection = new NetworkConnectionToServer();
            connection.SetHandlers(handlers);
        }

        static void InitializeTransportHandlers()
        {
            Transport.activeTransport.OnClientConnected.AddListener(OnConnected);
            Transport.activeTransport.OnClientDataReceived.AddListener(OnDataReceived);
            Transport.activeTransport.OnClientDisconnected.AddListener(OnDisconnected);
            Transport.activeTransport.OnClientError.AddListener(OnError);
        }

        static void OnError(Exception exception)
        {
            Debug.LogException(exception);
        }

        static void OnDisconnected()
        {
            connectState = ConnectState.Disconnected;

            ClientScene.HandleClientDisconnect(connection);

            connection?.InvokeHandler(new DisconnectMessage());
        }

        internal static void OnDataReceived(ArraySegment<byte> data)
        {
            if (connection != null)
            {
                connection.TransportReceive(data);
            }
            else Debug.LogError("Skipped Data message handling because connection is null.");
        }

        static void OnConnected()
        {
            if (connection != null)
            {
                // reset network time stats
                NetworkTime.Reset();

                // the handler may want to send messages to the client
                // thus we should set the connected state before calling the handler
                connectState = ConnectState.Connected;
                NetworkTime.UpdateClient();
                connection.InvokeHandler(new ConnectMessage());
            }
            else Debug.LogError("Skipped Connect message handling because connection is null.");
        }

        /// <summary>
        /// Disconnect from server.
        /// <para>The disconnect message will be invoked.</para>
        /// </summary>
        public static void Disconnect()
        {
            connectState = ConnectState.Disconnected;
            ClientScene.HandleClientDisconnect(connection);

            if (connection != null)
            {
                connection.Disconnect();
                connection = null;
                RemoveTransportHandlers();
            }
        }

        static void RemoveTransportHandlers()
        {
            // so that we don't register them more than once
            Transport.activeTransport.OnClientConnected.RemoveListener(OnConnected);
            Transport.activeTransport.OnClientDataReceived.RemoveListener(OnDataReceived);
            Transport.activeTransport.OnClientDisconnected.RemoveListener(OnDisconnected);
            Transport.activeTransport.OnClientError.RemoveListener(OnError);
        }

        /// <summary>
        /// This sends a network message with a message Id to the server. This message is sent on channel zero, which by default is the reliable channel.
        /// <para>The message must be an instance of a class derived from MessageBase.</para>
        /// <para>The message id passed to Send() is used to identify the handler function to invoke on the server when the message is received.</para>
        /// </summary>
        /// <typeparam name="T">The message type to unregister.</typeparam>
        /// <param name="message"></param>
        /// <param name="channelId"></param>
        /// <returns>True if message was sent.</returns>
        public static bool Send<T>(T message, int channelId = Channels.DefaultReliable)
            where T : struct, NetworkMessage
        {
            if (connection != null)
            {
                if (connectState != ConnectState.Connected)
                {
                    Debug.LogError("NetworkClient Send when not connected to a server");
                    return false;
                }
                return connection.Send(message, channelId);
            }
            Debug.LogError("NetworkClient Send with no connection");
            return false;
        }

        public static void Update()
        {
            // only update things while connected
            if (active && connectState == ConnectState.Connected)
            {
                NetworkTime.UpdateClient();
            }
        }

        internal static void RegisterSystemHandlers()
        {
            RegisterHandler<ObjectDestroyMessage>(ClientScene.OnObjectDestroy);
            RegisterHandler<ObjectHideMessage>(ClientScene.OnObjectHide);
            RegisterHandler<NetworkPongMessage>(NetworkTime.OnClientPong, false);
            RegisterHandler<SpawnMessage>(ClientScene.OnSpawn);
            RegisterHandler<UpdateVarsMessage>(ClientScene.OnUpdateVarsMessage);
            RegisterHandler<RpcMessage>(ClientScene.OnRPCMessage);
        }

        /// <summary>
        /// Register a handler for a particular message type.
        /// <para>There are several system message types which you can add handlers for. You can also add your own message types.</para>
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="handler">Function handler which will be invoked when this message type is received.</param>
        /// <param name="requireAuthentication">True if the message requires an authenticated connection</param>
        public static void RegisterHandler<T>(Action<NetworkConnection, T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            int msgType = MessagePacker.GetId<T>();
            if (handlers.ContainsKey(msgType))
            {
                Debug.LogWarning($"NetworkClient.RegisterHandler replacing handler for {typeof(T).FullName}, id={msgType}. If replacement is intentional, use ReplaceHandler instead to avoid this warning.");
            }
            handlers[msgType] = MessagePacker.WrapHandler(handler, requireAuthentication);
        }

        /// <summary>
        /// Register a handler for a particular message type.
        /// <para>There are several system message types which you can add handlers for. You can also add your own message types.</para>
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="handler">Function handler which will be invoked when this message type is received.</param>
        /// <param name="requireAuthentication">True if the message requires an authenticated connection</param>
        public static void RegisterHandler<T>(Action<T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            RegisterHandler((NetworkConnection _, T value) => { handler(value); }, requireAuthentication);
        }

        /// <summary>
        /// Unregisters a network message handler.
        /// </summary>
        /// <typeparam name="T">The message type to unregister.</typeparam>
        public static bool UnregisterHandler<T>()
            where T : struct, NetworkMessage
        {
            // use int to minimize collisions
            int msgType = MessagePacker.GetId<T>();
            return handlers.Remove(msgType);
        }

        /// <summary>
        /// Shut down a client.
        /// <para>This should be done when a client is no longer going to be used.</para>
        /// </summary>
        public static void Shutdown()
        {
            Debug.Log("Shutting down client.");
            ClientScene.Shutdown();
            connectState = ConnectState.None;
            handlers.Clear();
            // disconnect the client connection.
            // we do NOT call Transport.Shutdown, because someone only called
            // NetworkClient.Shutdown. we can't assume that the server is
            // supposed to be shut down too!
            Transport.activeTransport.ClientDisconnect();
        }
    }
}
