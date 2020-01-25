using System;
using System.Collections.Generic;
using System.ComponentModel;
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

    // TODO make fully static after removing obsoleted singleton!
    /// <summary>
    /// This is a network client class used by the networking system. It contains a NetworkConnection that is used to connect to a network server.
    /// <para>The <see cref="NetworkClient">NetworkClient</see> handle connection state, messages handlers, and connection configuration. There can be many <see cref="NetworkClient">NetworkClient</see> instances in a process at a time, but only one that is connected to a game server (<see cref="NetworkServer">NetworkServer</see>) that uses spawned objects.</para>
    /// <para><see cref="NetworkClient">NetworkClient</see> has an internal update function where it handles events from the transport layer. This includes asynchronous connect events, disconnect events and incoming data from a server.</para>
    /// <para>The <see cref="NetworkManager">NetworkManager</see> has a NetworkClient instance that it uses for games that it starts, but the NetworkClient may be used by itself.</para>
    /// </summary>
    public class NetworkClient
    {
        /// <summary>
        /// Obsolete: Use <see cref="NetworkClient"/> directly.
        /// <para>Singleton isn't needed anymore, all functions are static now. For example: NetworkClient.Send(message) instead of NetworkClient.singleton.Send(message).</para>
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use NetworkClient directly. Singleton isn't needed anymore, all functions are static now. For example: NetworkClient.Send(message) instead of NetworkClient.singleton.Send(message).")]
        public static NetworkClient singleton = new NetworkClient();

        /// <summary>
        /// A list of all the active network clients in the current process.
        /// <para>This is NOT a list of all clients that are connected to the remote server, it is client instances on the local game.</para>
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use NetworkClient directly instead. There is always exactly one client.")]
        public static List<NetworkClient> allClients => new List<NetworkClient> { singleton };

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
        /// NetworkClient can connect to local server in host mode too
        /// </summary>
        public static bool isLocalClient => connection is ULocalConnectionToServer;

        /// <summary>
        /// Connect client to a NetworkServer instance.
        /// </summary>
        /// <param name="address"></param>
        public static void Connect(string address)
        {
            if (LogFilter.Debug) Debug.Log("Client Connect: " + address);

            RegisterSystemHandlers(false);
            Transport.activeTransport.enabled = true;
            InitializeTransportHandlers();

            connectState = ConnectState.Connecting;
            Transport.activeTransport.ClientConnect(address);

            // setup all the handlers
            connection = new NetworkConnectionToServer();
            connection.SetHandlers(handlers);
        }

        /// <summary>
        /// Connect client to a NetworkServer instance.
        /// </summary>
        /// <param name="uri">Address of the server to connect to</param>
        public static void Connect(Uri uri)
        {
            if (LogFilter.Debug) Debug.Log("Client Connect: " + uri);

            RegisterSystemHandlers(false);
            Transport.activeTransport.enabled = true;
            InitializeTransportHandlers();

            connectState = ConnectState.Connecting;
            Transport.activeTransport.ClientConnect(uri);

            // setup all the handlers
            connection = new NetworkConnectionToServer();
            connection.SetHandlers(handlers);
        }

        internal static void ConnectHost()
        {
            if (LogFilter.Debug) Debug.Log("Client Connect Host to Server");

            RegisterSystemHandlers(true);

            connectState = ConnectState.Connected;

            // create local connection objects and connect them
            ULocalConnectionToServer connectionToServer = new ULocalConnectionToServer();
            ULocalConnectionToClient connectionToClient = new ULocalConnectionToClient();
            connectionToServer.connectionToClient = connectionToClient;
            connectionToClient.connectionToServer = connectionToServer;

            connection = connectionToServer;
            connection.SetHandlers(handlers);

            // create server connection to local client
            NetworkServer.SetLocalConnection(connectionToClient);
        }
        /// <summary>
        /// connect host mode
        /// </summary>
        internal static void ConnectLocalServer()
        {
            NetworkServer.OnConnected(NetworkServer.localConnection);
            NetworkServer.localConnection.Send(new ConnectMessage());
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

            connection?.InvokeHandler(new DisconnectMessage(), -1);
        }

        internal static void OnDataReceived(ArraySegment<byte> data, int channelId)
        {
            if (connection != null)
            {
                connection.TransportReceive(data, channelId);
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
                connection.InvokeHandler(new ConnectMessage(), -1);
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

            // local or remote connection?
            if (isLocalClient)
            {
                if (isConnected)
                {
                    NetworkServer.localConnection.Send(new DisconnectMessage());
                }
                NetworkServer.RemoveLocalConnection();
            }
            else
            {
                if (connection != null)
                {
                    connection.Disconnect();
                    connection.Dispose();
                    connection = null;
                    RemoveTransportHandlers();
                }
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
        /// Obsolete: Use <see cref="Send{T}(T, int)"/> instead with no message id instead
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use SendMessage<T> instead with no message id instead")]
        public static bool Send(short msgType, MessageBase msg)
        {
            if (connection != null)
            {
                if (connectState != ConnectState.Connected)
                {
                    Debug.LogError("NetworkClient Send when not connected to a server");
                    return false;
                }
                return connection.Send(msgType, msg);
            }
            Debug.LogError("NetworkClient Send with no connection");
            return false;
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
        public static bool Send<T>(T message, int channelId = Channels.DefaultReliable) where T : IMessageBase
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

        internal static void Update()
        {
            // local connection?
            if (connection is ULocalConnectionToServer localConnection)
            {
                localConnection.Update();
            }
            // remote connection?
            else
            {
                // only update things while connected
                if (active && connectState == ConnectState.Connected)
                {
                    NetworkTime.UpdateClient();
                }
            }
        }

        /* TODO use or remove
        void GenerateConnectError(byte error)
        {
            Debug.LogError("Mirror Client Error Connect Error: " + error);
            GenerateError(error);
        }

        void GenerateDataError(byte error)
        {
            NetworkError dataError = (NetworkError)error;
            Debug.LogError("Mirror Client Data Error: " + dataError);
            GenerateError(error);
        }

        void GenerateDisconnectError(byte error)
        {
            NetworkError disconnectError = (NetworkError)error;
            Debug.LogError("Mirror Client Disconnect Error: " + disconnectError);
            GenerateError(error);
        }

        void GenerateError(byte error)
        {
            int msgId = MessageBase.GetId<ErrorMessage>();
            if (handlers.TryGetValue(msgId, out NetworkMessageDelegate msgDelegate))
            {
                ErrorMessage msg = new ErrorMessage
                {
                    value = error
                };

                // write the message to a local buffer
                NetworkWriter writer = new NetworkWriter();
                msg.Serialize(writer);

                NetworkMessage netMsg = new NetworkMessage
                {
                    msgType = msgId,
                    reader = new NetworkReader(writer.ToArray()),
                    conn = connection
                };
                msgDelegate(netMsg);
            }
        }
        */

        /// <summary>
        /// Obsolete: Use <see cref="NetworkTime.rtt"/> instead
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use NetworkTime.rtt instead")]
        public static float GetRTT()
        {
            return (float)NetworkTime.rtt;
        }

        internal static void RegisterSystemHandlers(bool hostMode)
        {
            // host mode client / regular client react to some messages differently.
            // but we still need to add handlers for all of them to avoid
            // 'message id not found' errors.
            if (hostMode)
            {
                RegisterHandler<ObjectDestroyMessage>(ClientScene.OnHostClientObjectDestroy);
                RegisterHandler<ObjectHideMessage>(ClientScene.OnHostClientObjectHide);
                RegisterHandler<NetworkPongMessage>((conn, msg) => { }, false);
                RegisterHandler<SpawnMessage>(ClientScene.OnHostClientSpawn);
                RegisterHandler<ObjectSpawnStartedMessage>((conn, msg) => { }); // host mode doesn't need spawning
                RegisterHandler<ObjectSpawnFinishedMessage>((conn, msg) => { }); // host mode doesn't need spawning
                RegisterHandler<UpdateVarsMessage>((conn, msg) => { });
            }
            else
            {
                RegisterHandler<ObjectDestroyMessage>(ClientScene.OnObjectDestroy);
                RegisterHandler<ObjectHideMessage>(ClientScene.OnObjectHide);
                RegisterHandler<NetworkPongMessage>(NetworkTime.OnClientPong, false);
                RegisterHandler<SpawnMessage>(ClientScene.OnSpawn);
                RegisterHandler<ObjectSpawnStartedMessage>(ClientScene.OnObjectSpawnStarted);
                RegisterHandler<ObjectSpawnFinishedMessage>(ClientScene.OnObjectSpawnFinished);
                RegisterHandler<UpdateVarsMessage>(ClientScene.OnUpdateVarsMessage);
            }
            RegisterHandler<RpcMessage>(ClientScene.OnRPCMessage);
            RegisterHandler<SyncEventMessage>(ClientScene.OnSyncEventMessage);
        }

        /// <summary>
        /// Obsolete: Use <see cref="RegisterHandler{T}(Action{NetworkConnection, T}, bool)"/> instead
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use RegisterHandler<T> instead")]
        public static void RegisterHandler(int msgType, NetworkMessageDelegate handler)
        {
            if (handlers.ContainsKey(msgType))
            {
                if (LogFilter.Debug) Debug.Log("NetworkClient.RegisterHandler replacing " + handler + " - " + msgType);
            }
            handlers[msgType] = handler;
        }

        /// <summary>
        /// Obsolete: Use <see cref="RegisterHandler{T}(Action{NetworkConnection, T}, bool)"/> instead
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use RegisterHandler<T> instead")]
        public static void RegisterHandler(MsgType msgType, NetworkMessageDelegate handler)
        {
            RegisterHandler((int)msgType, handler);
        }

        /// <summary>
        /// Register a handler for a particular message type.
        /// <para>There are several system message types which you can add handlers for. You can also add your own message types.</para>
        /// </summary>
        /// <typeparam name="T">The message type to unregister.</typeparam>
        /// <param name="handler"></param>
        /// <param name="requireAuthentication">true if the message requires an authenticated connection</param>
        public static void RegisterHandler<T>(Action<NetworkConnection, T> handler, bool requireAuthentication = true) where T : IMessageBase, new()
        {
            int msgType = MessagePacker.GetId<T>();
            if (handlers.ContainsKey(msgType))
            {
                if (LogFilter.Debug) Debug.Log("NetworkClient.RegisterHandler replacing " + handler + " - " + msgType);
            }
            handlers[msgType] = MessagePacker.MessageHandler<T>(handler, requireAuthentication);
        }

        /// <summary>
        /// Register a handler for a particular message type.
        /// <para>There are several system message types which you can add handlers for. You can also add your own message types.</para>
        /// </summary>
        /// <typeparam name="T">The message type to unregister.</typeparam>
        /// <param name="handler"></param>
        /// <param name="requireAuthentication">true if the message requires an authenticated connection</param>
        public static void RegisterHandler<T>(Action<T> handler, bool requireAuthentication = true) where T : IMessageBase, new()
        {
            RegisterHandler((NetworkConnection _, T value) => { handler(value); }, requireAuthentication);
        }

        /// <summary>
        /// Obsolete: Use <see cref="UnregisterHandler{T}"/> instead
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use UnregisterHandler<T> instead")]
        public static void UnregisterHandler(int msgType)
        {
            handlers.Remove(msgType);
        }

        /// <summary>
        /// Obsolete: Use <see cref="UnregisterHandler{T}"/> instead
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use UnregisterHandler<T> instead")]
        public static void UnregisterHandler(MsgType msgType)
        {
            UnregisterHandler((int)msgType);
        }

        /// <summary>
        /// Unregisters a network message handler.
        /// </summary>
        /// <typeparam name="T">The message type to unregister.</typeparam>
        public static void UnregisterHandler<T>() where T : IMessageBase
        {
            // use int to minimize collisions
            int msgType = MessagePacker.GetId<T>();
            handlers.Remove(msgType);
        }

        /// <summary>
        /// Shut down a client.
        /// <para>This should be done when a client is no longer going to be used.</para>
        /// </summary>
        public static void Shutdown()
        {
            if (LogFilter.Debug) Debug.Log("Shutting down client.");
            ClientScene.Shutdown();
            connectState = ConnectState.None;
            handlers.Clear();
            // disconnect the client connection.
            // we do NOT call Transport.Shutdown, because someone only called
            // NetworkClient.Shutdown. we can't assume that the server is
            // supposed to be shut down too!
            Transport.activeTransport.ClientDisconnect();
        }

        /// <summary>
        /// Obsolete: Call <see cref="NetworkClient.Shutdown"/> instead. There is only one client.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Call NetworkClient.Shutdown() instead. There is only one client.")]
        public static void ShutdownAll()
        {
            Shutdown();
        }
    }
}
