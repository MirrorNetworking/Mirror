using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public class NetworkClient
    {
        // the client (can be a regular NetworkClient or a LocalClient)
        public static NetworkClient singleton;

        [Obsolete("Use NetworkClient.singleton instead. There is always exactly one client.")]
        public static List<NetworkClient> allClients => new List<NetworkClient>{singleton};

        public readonly Dictionary<int, NetworkMessageDelegate> handlers = new Dictionary<int, NetworkMessageDelegate>();

        public NetworkConnection connection { get; protected set; }

        protected enum ConnectState
        {
            None,
            Connecting,
            Connected,
            Disconnected
        }
        protected ConnectState connectState = ConnectState.None;

        public string serverIp { get; private set; } = "";

        // active is true while a client is connecting/connected
        // (= while the network is active)
        public static bool active { get; protected set; }

        public bool isConnected => connectState == ConnectState.Connected;

        public NetworkClient()
        {
            if (LogFilter.Debug) Debug.Log("Client created version " + Version.Current);

            if (singleton != null)
            {
                Debug.LogError("NetworkClient: can only create one!");
                return;
            }
            singleton = this;
        }

        internal void SetHandlers(NetworkConnection conn)
        {
            conn.SetHandlers(handlers);
        }

        public void Connect(string ip)
        {
            if (LogFilter.Debug) Debug.Log("Client Connect: " + ip);

            active = true;
            RegisterSystemHandlers(false);
            Transport.activeTransport.enabled = true;
            InitializeTransportHandlers();

            serverIp = ip;

            connectState = ConnectState.Connecting;
            Transport.activeTransport.ClientConnect(ip);

            // setup all the handlers
            connection = new NetworkConnection(serverIp, 0);
            connection.SetHandlers(handlers);
        }

        private void InitializeTransportHandlers()
        {
            Transport.activeTransport.OnClientConnected.AddListener(OnConnected);
            Transport.activeTransport.OnClientDataReceived.AddListener(OnDataReceived);
            Transport.activeTransport.OnClientDisconnected.AddListener(OnDisconnected);
            Transport.activeTransport.OnClientError.AddListener(OnError);
        }

        void OnError(Exception exception)
        {
            Debug.LogException(exception);
        }

        void OnDisconnected()
        {
            connectState = ConnectState.Disconnected;

            ClientScene.HandleClientDisconnect(connection);

            connection?.InvokeHandler(new DisconnectMessage());
        }

        protected void OnDataReceived(byte[] data)
        {
            if (connection != null)
            {
                connection.TransportReceive(data);
            }
            else Debug.LogError("Skipped Data message handling because m_Connection is null.");
        }

        void OnConnected()
        {
            if (connection != null)
            {
                // reset network time stats
                NetworkTime.Reset();

                // the handler may want to send messages to the client
                // thus we should set the connected state before calling the handler
                connectState = ConnectState.Connected;
                NetworkTime.UpdateClient(this);
                connection.InvokeHandler(new ConnectMessage());
            }
            else Debug.LogError("Skipped Connect message handling because m_Connection is null.");
        }

        public virtual void Disconnect()
        {
            connectState = ConnectState.Disconnected;
            ClientScene.HandleClientDisconnect(connection);
            if (connection != null)
            {
                connection.Disconnect();
                connection.Dispose();
                connection = null;
                RemoveTransportHandlers();
            }

            // the client's network is not active anymore.
            active = false;
        }

        void RemoveTransportHandlers()
        {
            // so that we don't register them more than once
            Transport.activeTransport.OnClientConnected.RemoveListener(OnConnected);
            Transport.activeTransport.OnClientDataReceived.RemoveListener(OnDataReceived);
            Transport.activeTransport.OnClientDisconnected.RemoveListener(OnDisconnected);
            Transport.activeTransport.OnClientError.RemoveListener(OnError);
        }

        [Obsolete("Use SendMessage<T> instead with no message id instead")]
        public bool Send(short msgType, MessageBase msg)
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

        public bool Send<T>(T message) where T : MessageBase
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

        internal virtual void Update()
        {
            // only update things while connected
            if (active && connectState == ConnectState.Connected)
            {
                NetworkTime.UpdateClient(this);
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

        [Obsolete("Use NetworkTime.rtt instead")]
        public float GetRTT()
        {
            return (float)NetworkTime.rtt;
        }

        internal void RegisterSystemHandlers(bool localClient)
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
                RegisterHandler<ObjectSpawnStartedMessage>((conn, msg) => {});
                RegisterHandler<ObjectSpawnFinishedMessage>((conn, msg) => {});
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

        [Obsolete("Use RegisterHandler<T> instead")]
        public void RegisterHandler(int msgType, NetworkMessageDelegate handler)
        {
            if (handlers.ContainsKey(msgType))
            {
                if (LogFilter.Debug) Debug.Log("NetworkClient.RegisterHandler replacing " + msgType);
            }
            handlers[msgType] = handler;
        }

        [Obsolete("Use RegisterHandler<T> instead")]
        public void RegisterHandler(MsgType msgType, NetworkMessageDelegate handler)
        {
            RegisterHandler((int)msgType, handler);
        }

        public void RegisterHandler<T>(Action<NetworkConnection, T> handler) where T : MessageBase, new()
        {
            int msgType = MessagePacker.GetId<T>();
            if (handlers.ContainsKey(msgType))
            {
                if (LogFilter.Debug) Debug.Log("NetworkClient.RegisterHandler replacing " + msgType);
            }
            handlers[msgType] = (networkMessage) =>
            {
                handler(networkMessage.conn, networkMessage.ReadMessage<T>());
            };
        }

        [Obsolete("Use UnregisterHandler<T> instead")]
        public void UnregisterHandler(int msgType)
        {
            handlers.Remove(msgType);
        }

        [Obsolete("Use UnregisterHandler<T> instead")]
        public void UnregisterHandler(MsgType msgType)
        {
            UnregisterHandler((int)msgType);
        }

        public void UnregisterHandler<T>() where T : MessageBase
        {
            // use int to minimize collisions
            int msgType = MessagePacker.GetId<T>();
            handlers.Remove(msgType);
        }

        internal static void UpdateClient()
        {
            singleton?.Update();
        }

        public void Shutdown()
        {
            if (LogFilter.Debug) Debug.Log("Shutting down client.");
            singleton = null;
            active = false;
        }

        public static void ShutdownAll()
        {
            singleton?.Shutdown();
            singleton = null;
            active = false;
            ClientScene.Shutdown();
        }
    }
}
