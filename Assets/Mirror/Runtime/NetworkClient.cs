using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public class NetworkClient
    {
        // the client (can be a regular NetworkClient or a LocalClient)
        public static NetworkClient singleton;

        int clientId = -1;

        public readonly Dictionary<short, NetworkMessageDelegate> handlers = new Dictionary<short, NetworkMessageDelegate>();

        public NetworkConnection connection { get; protected set; }

        protected enum ConnectState
        {
            None,
            Connecting,
            Connected,
            Disconnected,
        }
        protected ConnectState connectState = ConnectState.None;

        public string serverIp { get; private set; } = "";

        // active is true while a client is connecting/connected
        // (= while the network is active)
        public static bool active { get; protected set; }

        public bool isConnected => connectState == ConnectState.Connected;

        public NetworkClient()
        {
            if (LogFilter.Debug) { Debug.Log("Client created version " + Version.Current); }

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
            PrepareForConnect();

            if (LogFilter.Debug) { Debug.Log("Client Connect: " + ip); }

            serverIp = ip;

            connectState = ConnectState.Connecting;
            NetworkManager.singleton.transport.ClientConnect(ip);

            // setup all the handlers
            connection = new NetworkConnection(serverIp, clientId, 0);
            connection.SetHandlers(handlers);
        }

        private void InitializeTransportHandlers()
        {
            // TODO do this in inspector?
            NetworkManager.singleton.transport.OnClientConnected.AddListener(OnConnected);
            NetworkManager.singleton.transport.OnClientDataReceived.AddListener(OnDataReceived);
            NetworkManager.singleton.transport.OnClientDisconnected.AddListener(OnDisconnected);
            NetworkManager.singleton.transport.OnClientError.AddListener(OnError);
        }

        void OnError(Exception exception)
        {
            Debug.LogException(exception);
        }

        void OnDisconnected()
        {
            connectState = ConnectState.Disconnected;

            ClientScene.HandleClientDisconnect(connection);

            connection?.InvokeHandlerNoData((short)MsgType.Disconnect);
        }

        void OnDataReceived(byte[] data)
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
                connection.InvokeHandlerNoData((short)MsgType.Connect);
            }
            else Debug.LogError("Skipped Connect message handling because m_Connection is null.");
        }

        void PrepareForConnect()
        {
            active = true;
            RegisterSystemHandlers(false);
            clientId = 0;
            NetworkManager.singleton.transport.enabled = true;
            InitializeTransportHandlers();
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
                clientId = -1;
                RemoveTransportHandlers();
            }
        }

        void RemoveTransportHandlers()
        {
            // so that we don't register them more than once
            NetworkManager.singleton.transport.OnClientConnected.RemoveListener(OnConnected);
            NetworkManager.singleton.transport.OnClientDataReceived.RemoveListener(OnDataReceived);
            NetworkManager.singleton.transport.OnClientDisconnected.RemoveListener(OnDisconnected);
            NetworkManager.singleton.transport.OnClientError.RemoveListener(OnError);
        }

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

        public void Shutdown()
        {
            if (LogFilter.Debug) Debug.Log("Shutting down client " + clientId);
            clientId = -1;
            singleton = null;
            active = false;
        }

        internal virtual void Update()
        {
            if (clientId == -1)
            {
                return;
            }

            // only update things while connected
            if (connectState == ConnectState.Connected)
            {
                NetworkTime.UpdateClient(this);
            }
        }

        /* TODO use or remove
        void GenerateConnectError(byte error)
        {
            Debug.LogError("UNet Client Error Connect Error: " + error);
            GenerateError(error);
        }

        void GenerateDataError(byte error)
        {
            NetworkError dataError = (NetworkError)error;
            Debug.LogError("UNet Client Data Error: " + dataError);
            GenerateError(error);
        }

        void GenerateDisconnectError(byte error)
        {
            NetworkError disconnectError = (NetworkError)error;
            Debug.LogError("UNet Client Disconnect Error: " + disconnectError);
            GenerateError(error);
        }

        void GenerateError(byte error)
        {
            if (handlers.TryGetValue((short)MsgType.Error, out NetworkMessageDelegate msgDelegate))
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
                    msgType = (short)MsgType.Error,
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
            ClientScene.RegisterSystemHandlers(this, localClient);
        }

        public void RegisterHandler(short msgType, NetworkMessageDelegate handler)
        {
            if (handlers.ContainsKey(msgType))
            {
                if (LogFilter.Debug) { Debug.Log("NetworkClient.RegisterHandler replacing " + msgType); }
            }
            handlers[msgType] = handler;
        }

        public void RegisterHandler(MsgType msgType, NetworkMessageDelegate handler)
        {
            RegisterHandler((short)msgType, handler);
        }

        public void UnregisterHandler(short msgType)
        {
            handlers.Remove(msgType);
        }

        public void UnregisterHandler(MsgType msgType)
        {
            UnregisterHandler((short)msgType);
        }

        internal static void UpdateClients()
        {
            singleton?.Update();
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
