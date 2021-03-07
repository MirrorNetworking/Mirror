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

    /// <summary>NetworkClient with connection to server.</summary>
    public static class NetworkClient
    {
        // message handlers by messageId
        static readonly Dictionary<int, NetworkMessageDelegate> handlers =
            new Dictionary<int, NetworkMessageDelegate>();

        /// <summary>Client's NetworkConnection to server.</summary>
        public static NetworkConnection connection { get; internal set; }

        // NetworkClient state
        internal static ConnectState connectState = ConnectState.None;

        /// <summary>IP address of the connection to server.</summary>
        // empty if the client has not connected yet.
        public static string serverIp => connection.address;

        /// <summary>active is true while a client is connecting/connected</summary>
        // (= while the network is active)
        public static bool active => connectState == ConnectState.Connecting ||
                                     connectState == ConnectState.Connected;

        /// <summary>Check if client is connecting (before connected).</summary>
        public static bool isConnecting => connectState == ConnectState.Connecting;

        /// <summary>Check if client is connected (after connecting).</summary>
        public static bool isConnected => connectState == ConnectState.Connected;

        /// <summary>NetworkClient can connect to local server in host mode too</summary>
        public static bool isLocalClient => connection is LocalConnectionToServer;

        // OnConnected / OnDisconnected used to be NetworkMessages that were
        // invoked. this introduced a bug where external clients could send
        // Connected/Disconnected messages over the network causing undefined
        // behaviour.
        internal static Action<NetworkConnection> OnConnectedEvent;
        internal static Action<NetworkConnection> OnDisconnectedEvent;

        /// <summary>Connect client to a NetworkServer by address.</summary>
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

        /// <summary>Connect client to a NetworkServer by Uri.</summary>
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

        /// <summary>Connect host mode</summary>
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

        /// <summary>Disconnect host mode.</summary>
        // this is needed to call DisconnectMessage for the host client too.
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

        static void OnError(Exception exception) => Debug.LogException(exception);

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

        /// <summary>Disconnect from server.</summary>
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

        /// <summary>Send a NetworkMessage to the server over the given channel.</summary>
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

        // message handlers ////////////////////////////////////////////////////
        /// <summary>Register a handler for a message type T. Most should require authentication.</summary>
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

        /// <summary>Register a handler for a message type T. Most should require authentication.</summary>
        public static void RegisterHandler<T>(Action<T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            RegisterHandler((NetworkConnection _, T value) => { handler(value); }, requireAuthentication);
        }

        /// <summary>Replace a handler for a particular message type. Should require authentication by default.</summary>
        public static void ReplaceHandler<T>(Action<NetworkConnection, T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            int msgType = MessagePacking.GetId<T>();
            handlers[msgType] = MessagePacking.WrapHandler(handler, requireAuthentication);
        }

        /// <summary>Replace a handler for a particular message type. Should require authentication by default.</summary>
        public static void ReplaceHandler<T>(Action<T> handler, bool requireAuthentication = true)
            where T : struct, NetworkMessage
        {
            ReplaceHandler((NetworkConnection _, T value) => { handler(value); }, requireAuthentication);
        }

        /// <summary>Unregister a message handler of type T.</summary>
        public static bool UnregisterHandler<T>()
            where T : struct, NetworkMessage
        {
            // use int to minimize collisions
            int msgType = MessagePacking.GetId<T>();
            return handlers.Remove(msgType);
        }

        // update //////////////////////////////////////////////////////////////
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

        // shutdown ////////////////////////////////////////////////////////////
        /// <summary>Shutdown the client.</summary>
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
