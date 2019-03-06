// wraps UNET's LLAPI for use as HLAPI TransportLayer
using System;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.Types;

namespace Mirror
{
    [Obsolete("LLAPI is obsolete and will be removed from future versions of Unity")]
    public class LLAPITransport : Transport
    {
        public ushort port = 7777;

        [Tooltip("Enable for WebGL games. Can only do either WebSockets or regular Sockets, not both (yet).")]
        public bool useWebsockets;

        // settings copied from uMMORPG configuration for best results
        public ConnectionConfig connectionConfig = new ConnectionConfig
        {
            PacketSize = 1500,
            FragmentSize = 500,
            ResendTimeout = 1200,
            DisconnectTimeout = 6000,
            ConnectTimeout = 6000,
            MinUpdateTimeout = 1,
            PingTimeout = 2000,
            ReducedPingTimeout = 100,
            AllCostTimeout = 20,
            NetworkDropThreshold = 80,
            OverflowDropThreshold = 80,
            MaxConnectionAttempt = 10,
            AckDelay = 33,
            SendDelay = 10,
            MaxCombinedReliableMessageSize = 100,
            MaxCombinedReliableMessageCount = 10,
            MaxSentMessageQueueSize = 512,
            AcksType = ConnectionAcksType.Acks128,
            InitialBandwidth = 0,
            BandwidthPeakFactor = 2,
            WebSocketReceiveBufferMaxSize = 0,
            UdpSocketReceiveBufferMaxSize = 0
        };

        // settings copied from uMMORPG configuration for best results
        public GlobalConfig globalConfig = new GlobalConfig
        {
            ReactorModel = ReactorModel.SelectReactor,
            ThreadAwakeTimeout = 1,
            ReactorMaximumSentMessages = 4096,
            ReactorMaximumReceivedMessages = 4096,
            MaxPacketSize = 2000,
            MaxHosts = 16,
            ThreadPoolSize = 3,
            MinTimerTimeout = 1,
            MaxTimerTimeout = 12000
        };

        readonly int channelId; // always use first channel
        byte error;

        int clientId = -1;
        int clientConnectionId = -1;
        readonly byte[] clientReceiveBuffer = new byte[4096];

        int serverHostId = -1;
        readonly byte[] serverReceiveBuffer = new byte[4096];

        void OnValidate()
        {
            // add connectionconfig channels if none
            if (connectionConfig.Channels.Count == 0)
            {
                // channel 0 is reliable fragmented sequenced
                connectionConfig.AddChannel(QosType.ReliableFragmentedSequenced);
                // channel 1 is unreliable
                connectionConfig.AddChannel(QosType.Unreliable);
            }
        }

        void Awake()
        {
            NetworkTransport.Init(globalConfig);
            Debug.Log("LLAPITransport initialized!");
        }

        #region client
        public override bool ClientConnected()
        {
            return clientConnectionId != -1;
        }

        public override void ClientConnect(string address)
        {
            // LLAPI can't handle 'localhost'
            if (address.ToLower() == "localhost") address = "127.0.0.1";

            HostTopology hostTopology = new HostTopology(connectionConfig, 1);

            // important:
            //   AddHost(topology) doesn't work in WebGL.
            //   AddHost(topology, port) works in standalone and webgl if port=0
            clientId = NetworkTransport.AddHost(hostTopology, 0);

            clientConnectionId = NetworkTransport.Connect(clientId, address, port, 0, out error);
            NetworkError networkError = (NetworkError)error;
            if (networkError != NetworkError.Ok)
            {
                Debug.LogWarning("NetworkTransport.Connect failed: clientId=" + clientId + " address= " + address + " port=" + port + " error=" + error);
                clientConnectionId = -1;
            }
        }

        public override bool ClientSend(int channelId, byte[] data)
        {
            return NetworkTransport.Send(clientId, clientConnectionId, channelId, data, data.Length, out error);
        }

        public bool ProcessClientMessage()
        {
            if (clientId == -1) return false;

            NetworkEventType networkEvent = NetworkTransport.ReceiveFromHost(clientId, out int connectionId, out int channel, clientReceiveBuffer, clientReceiveBuffer.Length, out int receivedSize, out error);

            // note: 'error' is used for extra information, e.g. the reason for
            // a disconnect. we don't necessarily have to throw an error if
            // error != 0. but let's log it for easier debugging.
            //
            // DO NOT return after error != 0. otherwise Disconnect won't be
            // registered.
            NetworkError networkError = (NetworkError)error;
            if (networkError != NetworkError.Ok)
            {
                string message = "NetworkTransport.Receive failed: hostid=" + clientId + " connId=" + connectionId + " channelId=" + channel + " error=" + networkError;
                OnClientError.Invoke(new Exception(message));
            }

            // raise events
            switch (networkEvent)
            {
                case NetworkEventType.ConnectEvent:
                    OnClientConnected.Invoke();
                    break;
                case NetworkEventType.DataEvent:
                    byte[] data = new byte[receivedSize];
                    Array.Copy(clientReceiveBuffer, data, receivedSize);
                    OnClientDataReceived.Invoke(data);
                    break;
                case NetworkEventType.DisconnectEvent:
                    OnClientDisconnected.Invoke();
                    break;
                default:
                    return false;
            }

            return true;
        }

        public string ClientGetAddress()
        {
            NetworkTransport.GetConnectionInfo(serverHostId, clientId, out string address, out int port, out NetworkID networkId, out NodeID node, out error);
            return address;
        }

        public override void ClientDisconnect()
        {
            if (clientId != -1)
            {
                NetworkTransport.RemoveHost(clientId);
                clientId = -1;
            }
        }
        #endregion

        #region server
        public override bool ServerActive()
        {
            return serverHostId != -1;
        }

        public override void ServerStart()
        {
            if (useWebsockets)
            {
                HostTopology topology = new HostTopology(connectionConfig, ushort.MaxValue - 1);
                serverHostId = NetworkTransport.AddWebsocketHost(topology, port);
                //Debug.Log("LLAPITransport.ServerStartWebsockets port=" + port + " max=" + maxConnections + " hostid=" + serverHostId);
            }
            else
            {
                HostTopology topology = new HostTopology(connectionConfig, ushort.MaxValue - 1);
                serverHostId = NetworkTransport.AddHost(topology, port);
                //Debug.Log("LLAPITransport.ServerStart port=" + port + " max=" + maxConnections + " hostid=" + serverHostId);
            }
        }

        public override bool ServerSend(int connectionId, int channelId, byte[] data)
        {
            return NetworkTransport.Send(serverHostId, connectionId, channelId, data, data.Length, out error);
        }

        public bool ProcessServerMessage()
        {
            if (serverHostId == -1) return false;

            int connectionId = -1;
            NetworkEventType networkEvent = NetworkTransport.ReceiveFromHost(serverHostId, out connectionId, out int channel, serverReceiveBuffer, serverReceiveBuffer.Length, out int receivedSize, out error);

            // note: 'error' is used for extra information, e.g. the reason for
            // a disconnect. we don't necessarily have to throw an error if
            // error != 0. but let's log it for easier debugging.
            //
            // DO NOT return after error != 0. otherwise Disconnect won't be
            // registered.
            NetworkError networkError = (NetworkError)error;
            if (networkError != NetworkError.Ok)
            {
                string message = "NetworkTransport.Receive failed: hostid=" + serverHostId + " connId=" + connectionId + " channelId=" + channel + " error=" + networkError;

                // TODO write a TransportException or better
                OnServerError.Invoke(connectionId, new Exception(message));
            }

            // LLAPI client sends keep alive messages (75-6C-6C) on channel=110.
            // ignore all messages that aren't for our selected channel.
            /*if (channel != channelId)
            {
                return false;
            }*/

            switch (networkEvent)
            {
                case NetworkEventType.ConnectEvent:
                    OnServerConnected.Invoke(connectionId);
                    break;
                case NetworkEventType.DataEvent:
                    byte[] data = new byte[receivedSize];
                    Array.Copy(serverReceiveBuffer, data, receivedSize);
                    OnServerDataReceived.Invoke(connectionId, data);
                    break;
                case NetworkEventType.DisconnectEvent:
                    OnServerDisconnected.Invoke(connectionId);
                    break;
                default:
                    // nothing or a message we don't recognize
                    return false;
            }

            return true;
        }

        public override bool ServerDisconnect(int connectionId)
        {
            return NetworkTransport.Disconnect(serverHostId, connectionId, out error);
        }

        public override string ServerGetClientAddress(int connectionId)
        {
            NetworkTransport.GetConnectionInfo(serverHostId, connectionId, out string address, out int port, out NetworkID networkId, out NodeID node, out error);
            return address;
        }

        public override void ServerStop()
        {
            NetworkTransport.RemoveHost(serverHostId);
            serverHostId = -1;
            Debug.Log("LLAPITransport.ServerStop");
        }
        #endregion

        #region common
        // IMPORTANT: set script execution order to >1000 to call Transport's
        //            LateUpdate after all others. Fixes race condition where
        //            e.g. in uSurvival Transport would apply Cmds before
        //            ShoulderRotation.LateUpdate, resulting in projectile
        //            spawns at the point before shoulder rotation.
        public void LateUpdate()
        {
            // process all messages
            while (ProcessClientMessage()) {}
            while (ProcessServerMessage()) {}
        }

        public override bool Available()
        {
            // websocket is available in all platforms (including webgl)
            return useWebsockets || base.Available();
        }

        public override void Shutdown()
        {
            NetworkTransport.Shutdown();
            serverHostId = -1;
            clientConnectionId = -1;
            Debug.Log("LLAPITransport.Shutdown");
        }

        public override int GetMaxPacketSize(int channelId)
        {
            return globalConfig.MaxPacketSize;
        }

        public override string ToString()
        {
            if (ServerActive())
            {
                return "LLAPI Server port: " + port;
            }
            else if (ClientConnected())
            {
                string ip = ClientGetAddress();
                return "LLAPI Client ip: " + ip + " port: " + port;
            }
            return "LLAPI (inactive/disconnected)";
        }
        #endregion
    }
}
