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
    public class NetworkClient
    {
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use NetworkClient directly. Singleton isn't needed anymore, all functions are static now. For example: NetworkClient.Send(message) instead of NetworkClient.singleton.Send(message).")]
        public static NetworkClient singleton = new NetworkClient();

        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use NetworkClient directly instead. There is always exactly one client.")]
        public static List<NetworkClient> allClients => new List<NetworkClient>{singleton};

        public static readonly Dictionary<int, NetworkMessageDelegate> handlers = new Dictionary<int, NetworkMessageDelegate>();

        public static NetworkConnection connection { get; internal set; }

        internal static ConnectState connectState = ConnectState.None;

        public static string serverIp => connection.address;

        // active is true while a client is connecting/connected
        // (= while the network is active)
        public static bool active => connectState == ConnectState.Connecting || connectState == ConnectState.Connected;

        public static bool isConnected => connectState == ConnectState.Connected;

        // NetworkClient can connect to local server in host mode too
        public static bool isLocalClient => connection is ULocalConnectionToServer;

        // local client in host mode might call Cmds/Rpcs during Update, but we
        // want to apply them in LateUpdate like all other Transport messages
        // to avoid race conditions. keep packets in Queue until LateUpdate.
        internal static Queue<byte[]> localClientPacketQueue = new Queue<byte[]>();

        // connect remote
        public static void Connect(string address)
        {
            if (LogFilter.Debug) Debug.Log("Client Connect: " + address);

            RegisterSystemHandlers(false);
            Transport.activeTransport.enabled = true;
            InitializeTransportHandlers();

            connectState = ConnectState.Connecting;
            Transport.activeTransport.ClientConnect(address);

            // setup all the handlers
            connection = new NetworkConnection(address, 0);
            connection.SetHandlers(handlers);
        }

        // connect host mode
        internal static void ConnectLocalServer()
        {
            if (LogFilter.Debug) Debug.Log("Client Connect Local Server");

            RegisterSystemHandlers(true);

            connectState = ConnectState.Connected;

            // create local connection to server
            connection = new ULocalConnectionToServer();
            connection.SetHandlers(handlers);

            // create server connection to local client
            ULocalConnectionToClient connectionToClient = new ULocalConnectionToClient();
            NetworkServer.SetLocalConnection(connectionToClient);

            localClientPacketQueue.Enqueue(MessagePacker.Pack(new ConnectMessage()));
        }

        // Called by the server to set the LocalClient's LocalPlayer object during NetworkServer.AddPlayer()
        internal static void AddLocalPlayer(NetworkIdentity localPlayer)
        {
            if (LogFilter.Debug) Debug.Log("Local client AddLocalPlayer " + localPlayer.gameObject.name + " conn=" + connection.connectionId);
            connection.isReady = true;
            connection.playerController = localPlayer;
            if (localPlayer != null)
            {
                localPlayer.isClient = true;
                NetworkIdentity.spawned[localPlayer.netId] = localPlayer;
                localPlayer.connectionToServer = connection;
            }
            // there is no SystemOwnerMessage for local client. add to ClientScene here instead
            ClientScene.InternalAddPlayer(localPlayer);
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

        internal static void OnDataReceived(byte[] data)
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

        public static void Disconnect()
        {
            connectState = ConnectState.Disconnected;
            ClientScene.HandleClientDisconnect(connection);

            // local or remote connection?
            if (isLocalClient)
            {
                if (isConnected)
                {
                    localClientPacketQueue.Enqueue(MessagePacker.Pack(new DisconnectMessage()));
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

        public static bool Send<T>(T message) where T : IMessageBase
        {
            if (connection != null)
            {
                if (connectState != ConnectState.Connected)
                {
                    Debug.LogError("NetworkClient Send when not connected to a server");
                    return false;
                }
                return connection.Send(message);
            }
            Debug.LogError("NetworkClient Send with no connection");
            return false;
        }

        internal static void Update()
        {
            // local or remote connection?
            if (isLocalClient)
            {
                // process internal messages so they are applied at the correct time
                while (localClientPacketQueue.Count > 0)
                {
                    byte[] packet = localClientPacketQueue.Dequeue();
                    OnDataReceived(packet);
                }
            }
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

        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use NetworkTime.rtt instead")]
        public static float GetRTT()
        {
            return (float)NetworkTime.rtt;
        }

        internal static void RegisterSystemHandlers(bool localClient)
        {
            // local client / regular client react to some messages differently.
            // but we still need to add handlers for all of them to avoid
            // 'message id not found' errors.
            if (localClient)
            {
                RegisterHandler<ObjectDestroyMessage>(ClientScene.OnLocalClientObjectDestroy);
                RegisterHandler<ObjectHideMessage>(ClientScene.OnLocalClientObjectHide);
                RegisterHandler<OwnerMessage>((conn, msg) => {});
                RegisterHandler<NetworkPongMessage>((conn, msg) => {});
                RegisterHandler<SpawnPrefabMessage>(ClientScene.OnLocalClientSpawnPrefab);
                RegisterHandler<SpawnSceneObjectMessage>(ClientScene.OnLocalClientSpawnSceneObject);
                RegisterHandler<ObjectSpawnStartedMessage>((conn, msg) => {}); // host mode doesn't need spawning
                RegisterHandler<ObjectSpawnFinishedMessage>((conn, msg) => {}); // host mode doesn't need spawning
                RegisterHandler<UpdateVarsMessage>((conn, msg) => {});
            }
            else
            {
                RegisterHandler<ObjectDestroyMessage>(ClientScene.OnObjectDestroy);
                RegisterHandler<ObjectHideMessage>(ClientScene.OnObjectHide);
                RegisterHandler<OwnerMessage>(ClientScene.OnOwnerMessage);
                RegisterHandler<NetworkPongMessage>(NetworkTime.OnClientPong);
                RegisterHandler<SpawnPrefabMessage>(ClientScene.OnSpawnPrefab);
                RegisterHandler<SpawnSceneObjectMessage>(ClientScene.OnSpawnSceneObject);
                RegisterHandler<ObjectSpawnStartedMessage>(ClientScene.OnObjectSpawnStarted);
                RegisterHandler<ObjectSpawnFinishedMessage>(ClientScene.OnObjectSpawnFinished);
                RegisterHandler<UpdateVarsMessage>(ClientScene.OnUpdateVarsMessage);
            }
            RegisterHandler<ClientAuthorityMessage>(ClientScene.OnClientAuthority);
            RegisterHandler<RpcMessage>(ClientScene.OnRPCMessage);
            RegisterHandler<SyncEventMessage>(ClientScene.OnSyncEventMessage);
        }

        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use RegisterHandler<T> instead")]
        public static void RegisterHandler(int msgType, NetworkMessageDelegate handler)
        {
            if (handlers.ContainsKey(msgType))
            {
                if (LogFilter.Debug) Debug.Log("NetworkClient.RegisterHandler replacing " + handler + " - " + msgType);
            }
            handlers[msgType] = handler;
        }

        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use RegisterHandler<T> instead")]
        public static void RegisterHandler(MsgType msgType, NetworkMessageDelegate handler)
        {
            RegisterHandler((int)msgType, handler);
        }

        public static void RegisterHandler<T>(Action<NetworkConnection, T> handler) where T : IMessageBase, new()
        {
            int msgType = MessagePacker.GetId<T>();
            if (handlers.ContainsKey(msgType))
            {
                if (LogFilter.Debug) Debug.Log("NetworkClient.RegisterHandler replacing " + handler + " - " + msgType);
            }
            handlers[msgType] = MessagePacker.MessageHandler<T>(handler);
        }

        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use UnregisterHandler<T> instead")]
        public static void UnregisterHandler(int msgType)
        {
            handlers.Remove(msgType);
        }

        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Use UnregisterHandler<T> instead")]
        public static void UnregisterHandler(MsgType msgType)
        {
            UnregisterHandler((int)msgType);
        }

        public static void UnregisterHandler<T>() where T : IMessageBase
        {
            // use int to minimize collisions
            int msgType = MessagePacker.GetId<T>();
            handlers.Remove(msgType);
        }

        public static void Shutdown()
        {
            if (LogFilter.Debug) Debug.Log("Shutting down client.");
            ClientScene.Shutdown();
            connectState = ConnectState.None;
        }

        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Call NetworkClient.Shutdown() instead. There is only one client.")]
        public static void ShutdownAll()
        {
            Shutdown();
        }
    }
}
