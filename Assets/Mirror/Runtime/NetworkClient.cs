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
        static readonly Dictionary<int, NetworkMessageDelegate> handlers =
            new Dictionary<int, NetworkMessageDelegate>();

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
        public static bool isLocalClient => connection is LocalConnectionToServer;

        // OnConnected / OnDisconnected used to be NetworkMessages that were
        // invoked. this introduced a bug where external clients could send
        // Connected/Disconnected messages over the network causing undefined
        // behaviour.
        internal static Action<NetworkConnection> OnConnectedEvent;
        internal static Action<NetworkConnection> OnDisconnectedEvent;

        /// <summary>
        /// Connect client to a NetworkServer instance.
        /// </summary>
        /// <param name="address"></param>
        public static void Connect(string address)
        {
            // Debug.Log("Client Connect: " + address);
            Debug.Assert(Transport.activeTransport != null, "There was no active transport when calling NetworkClient.Connect, If you are calling Connect manually then make sure to set 'Transport.activeTransport' first");

            RegisterSystemHandlers(false);
            Transport.activeTransport.enabled = true;
            AddTransportHandlers();

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
            // Debug.Log("Client Connect: " + uri);
            Debug.Assert(Transport.activeTransport != null, "There was no active transport when calling NetworkClient.Connect, If you are calling Connect manually then make sure to set 'Transport.activeTransport' first");

            RegisterSystemHandlers(false);
            Transport.activeTransport.enabled = true;
            AddTransportHandlers();

            connectState = ConnectState.Connecting;
            Transport.activeTransport.ClientConnect(uri);

            // setup all the handlers
            connection = new NetworkConnectionToServer();
            connection.SetHandlers(handlers);
        }

        public static void ConnectHost()
        {
            //Debug.Log("Client Connect Host to Server");

            RegisterSystemHandlers(true);

            connectState = ConnectState.Connected;

            // create local connection objects and connect them
            LocalConnectionToServer connectionToServer = new LocalConnectionToServer();
            LocalConnectionToClient connectionToClient = new LocalConnectionToClient();
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
        public static void ConnectLocalServer()
        {
            // call server OnConnected with server's connection to client
            NetworkServer.OnConnected(NetworkServer.localConnection);

            // call client OnConnected with client's connection to server
            // => previously we used to send a ConnectMessage to
            //    NetworkServer.localConnection. this would queue the message
            //    until NetworkClient.Update processes it.
            // => invoking the client's OnConnected event directly here makes
            //    tests fail. so let's do it exactly the same order as before by
            //    queueing the event for next Update!
            //OnConnectedEvent?.Invoke(connection);
            ((LocalConnectionToServer)connection).QueueConnectedEvent();
        }

        /// <summary>
        /// disconnect host mode. this is needed to call DisconnectMessage for
        /// the host client too.
        /// </summary>
        public static void DisconnectLocalServer()
        {
            // only if host connection is running
            if (NetworkServer.localConnection != null)
            {
                // TODO ConnectLocalServer manually sends a ConnectMessage to the
                // local connection. should we send a DisconnectMessage here too?
                // (if we do then we get an Unknown Message ID log)
                //NetworkServer.localConnection.Send(new DisconnectMessage());
                NetworkServer.OnDisconnected(NetworkServer.localConnection.connectionId);
            }
        }

        static void AddTransportHandlers()
        {
            Transport.activeTransport.OnClientConnected = OnConnected;
            Transport.activeTransport.OnClientDataReceived = OnDataReceived;
            Transport.activeTransport.OnClientDisconnected = OnDisconnected;
            Transport.activeTransport.OnClientError = OnError;
        }

        static void OnError(Exception exception)
        {
            Debug.LogException(exception);
        }

        static void OnDisconnected()
        {
            connectState = ConnectState.Disconnected;

            ClientScene.HandleClientDisconnect(connection);

            if (connection != null) OnDisconnectedEvent?.Invoke(connection);
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
                OnConnectedEvent?.Invoke(connection);
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
                    // call client OnDisconnected with connection to server
                    // => previously we used to send a DisconnectMessage to
                    //    NetworkServer.localConnection. this would queue the
                    //    message until NetworkClient.Update processes it.
                    // => invoking the client's OnDisconnected event directly
                    //    here makes tests fail. so let's do it exactly the same
                    //    order as before by queueing the event for next Update!
                    //OnDisconnectedEvent?.Invoke(connection);
                    ((LocalConnectionToServer)connection).QueueDisconnectedEvent();
                }
                NetworkServer.RemoveLocalConnection();
            }
            else
            {
                if (connection != null)
                {
                    connection.Disconnect();
                    connection = null;
                }
            }
        }

        /// <summary>
        /// This sends a network message with a message Id to the server. This message is sent on channel zero, which by default is the reliable channel.
        /// <para>The message must be an instance of a class derived from MessageBase.</para>
        /// <para>The message id passed to Send() is used to identify the handler function to invoke on the server when the message is received.</para>
        /// </summary>
        /// <typeparam name="T">The message type to unregister.</typeparam>
        /// <param name="message"></param>
        /// <param name="channelId"></param>
        public static void Send<T>(T message, int channelId = Channels.DefaultReliable)
            where T : struct, NetworkMessage
        {
            if (connection != null)
            {
                if (connectState == ConnectState.Connected)
                {
                    connection.Send(message, channelId);
                }
                else Debug.LogError("NetworkClient Send when not connected to a server");
            }
            else Debug.LogError("NetworkClient Send with no connection");
        }

        // NetworkEarlyUpdate called before any Update/FixedUpdate
        // (we add this to the UnityEngine in NetworkLoop)
        internal static void NetworkEarlyUpdate()
        {
            // process all incoming messages first before updating the world
            if (Transport.activeTransport != null)
                Transport.activeTransport.ClientEarlyUpdate();
        }

        // NetworkLateUpdate called after any Update/FixedUpdate/LateUpdate
        // (we add this to the UnityEngine in NetworkLoop)
        internal static void NetworkLateUpdate()
        {
            // local connection?
            if (connection is LocalConnectionToServer localConnection)
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

            // process all incoming messages after updating the world
            if (Transport.activeTransport != null)
                Transport.activeTransport.ClientLateUpdate();
        }

        // obsolete to not break people's projects. Update was public.
        [Obsolete("NetworkClient.Update is now called internally from our custom update loop. No need to call Update manually anymore.")]
        public static void Update() => NetworkLateUpdate();

        internal static void RegisterSystemHandlers(bool hostMode)
        {
            // host mode client / regular client react to some messages differently.
            // but we still need to add handlers for all of them to avoid
            // 'message id not found' errors.
            if (hostMode)
            {
                RegisterHandler<ObjectDestroyMessage>(ClientScene.OnHostClientObjectDestroy);
                RegisterHandler<ObjectHideMessage>(ClientScene.OnHostClientObjectHide);
                RegisterHandler<NetworkPongMessage>((conn, msg) => {}, false);
                RegisterHandler<SpawnMessage>(ClientScene.OnHostClientSpawn);
                // host mode doesn't need spawning
                RegisterHandler<ObjectSpawnStartedMessage>((conn, msg) => {});
                // host mode doesn't need spawning
                RegisterHandler<ObjectSpawnFinishedMessage>((conn, msg) => {});
                // host mode doesn't need state updates
                RegisterHandler<UpdateVarsMessage>((conn, msg) => {});
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
            int msgType = MessagePacking.GetId<T>();
            if (handlers.ContainsKey(msgType))
            {
                Debug.LogWarning($"NetworkClient.RegisterHandler replacing handler for {typeof(T).FullName}, id={msgType}. If replacement is intentional, use ReplaceHandler instead to avoid this warning.");
            }
            handlers[msgType] = MessagePacking.WrapHandler(handler, requireAuthentication);
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
        /// Replaces a handler for a particular message type.
        /// <para>See also <see cref="RegisterHandler{T}(Action{NetworkConnection, T}, bool)">RegisterHandler(T)(Action(NetworkConnection, T), bool)</see></para>
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="handler">Function handler which will be invoked when this message type is received.</param>
        /// <param name="requireAuthentication">True if the message requires an authenticated connection</param>
        public static void ReplaceHandler<T>(Action<NetworkConnection, T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            int msgType = MessagePacking.GetId<T>();
            handlers[msgType] = MessagePacking.WrapHandler(handler, requireAuthentication);
        }

        /// <summary>
        /// Replaces a handler for a particular message type.
        /// <para>See also <see cref="RegisterHandler{T}(Action{NetworkConnection, T}, bool)">RegisterHandler(T)(Action(NetworkConnection, T), bool)</see></para>
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="handler">Function handler which will be invoked when this message type is received.</param>
        /// <param name="requireAuthentication">True if the message requires an authenticated connection</param>
        public static void ReplaceHandler<T>(Action<T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            ReplaceHandler((NetworkConnection _, T value) => { handler(value); }, requireAuthentication);
        }

        /// <summary>
        /// Unregisters a network message handler.
        /// </summary>
        /// <typeparam name="T">The message type to unregister.</typeparam>
        public static bool UnregisterHandler<T>()
            where T : struct, NetworkMessage
        {
            // use int to minimize collisions
            int msgType = MessagePacking.GetId<T>();
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
