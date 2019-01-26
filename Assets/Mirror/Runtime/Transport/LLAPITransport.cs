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

        ConnectionConfig connectionConfig;
        GlobalConfig globalConfig;
        readonly int channelId; // always use first channel
        byte error;

        int clientId = -1;
        int clientConnectionId = -1;
        readonly byte[] clientReceiveBuffer = new byte[4096];

        int serverHostId = -1;
        readonly byte[] serverReceiveBuffer = new byte[4096];

        void Awake()
        {
            // create global config if none passed
            // -> settings copied from uMMORPG configuration for best results
            if (globalConfig == null)
            {
                globalConfig = new GlobalConfig();
                globalConfig.ReactorModel = ReactorModel.SelectReactor;
                globalConfig.ThreadAwakeTimeout = 1;
                globalConfig.ReactorMaximumSentMessages = 4096;
                globalConfig.ReactorMaximumReceivedMessages = 4096;
                globalConfig.MaxPacketSize = 2000;
                globalConfig.MaxHosts = 16;
                globalConfig.ThreadPoolSize = 3;
                globalConfig.MinTimerTimeout = 1;
                globalConfig.MaxTimerTimeout = 12000;
            }
            NetworkTransport.Init(globalConfig);

            // create connection config if none passed
            // -> settings copied from uMMORPG configuration for best results
            if (connectionConfig == null)
            {
                connectionConfig = new ConnectionConfig();
                connectionConfig.PacketSize = 1500;
                connectionConfig.FragmentSize = 500;
                connectionConfig.ResendTimeout = 1200;
                connectionConfig.DisconnectTimeout = 6000;
                connectionConfig.ConnectTimeout = 6000;
                connectionConfig.MinUpdateTimeout = 1;
                connectionConfig.PingTimeout = 2000;
                connectionConfig.ReducedPingTimeout = 100;
                connectionConfig.AllCostTimeout = 20;
                connectionConfig.NetworkDropThreshold = 80;
                connectionConfig.OverflowDropThreshold = 80;
                connectionConfig.MaxConnectionAttempt = 10;
                connectionConfig.AckDelay = 33;
                connectionConfig.SendDelay = 10;
                connectionConfig.MaxCombinedReliableMessageSize = 100;
                connectionConfig.MaxCombinedReliableMessageCount = 10;
                connectionConfig.MaxSentMessageQueueSize = 512;
                connectionConfig.AcksType = ConnectionAcksType.Acks128;
                connectionConfig.InitialBandwidth = 0;
                connectionConfig.BandwidthPeakFactor = 2;
                connectionConfig.WebSocketReceiveBufferMaxSize = 0;
                connectionConfig.UdpSocketReceiveBufferMaxSize = 0;
                // channel 0 is reliable fragmented sequenced
                connectionConfig.AddChannel(QosType.ReliableFragmentedSequenced);
                // channel 1 is unreliable
                connectionConfig.AddChannel(QosType.Unreliable);
            }

            Debug.Log("LLAPITransport initialized!");
        }

        // client //////////////////////////////////////////////////////////////
        public override bool ClientConnected()
        {
            return clientConnectionId != -1;
        }

        public override void ClientConnect(string address)
        {
            HostTopology hostTopology = new HostTopology(connectionConfig, 1);

            // important:
            //   AddHost(topology) doesn't work in WebGL.
            //   AddHost(topology, port) works in standalone and webgl if port=0
            clientId = NetworkTransport.AddHost(hostTopology, 0);

            clientConnectionId = NetworkTransport.Connect(clientId, address, port, 0, out error);
            NetworkError networkError = (NetworkError) error;
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

        public override bool ClientGetNextMessage(out TransportEvent transportEvent, out byte[] data)
        {
            transportEvent = TransportEvent.Disconnected;
            data = null;
            int connectionId;
            int channel;
            int receivedSize;
            NetworkEventType networkEvent = NetworkTransport.ReceiveFromHost(clientId, out connectionId, out channel, clientReceiveBuffer, clientReceiveBuffer.Length, out receivedSize, out error);

            // note: 'error' is used for extra information, e.g. the reason for
            // a disconnect. we don't necessarily have to throw an error if
            // error != 0. but let's log it for easier debugging.
            //
            // DO NOT return after error != 0. otherwise Disconnect won't be
            // registered.
            NetworkError networkError = (NetworkError)error;
            if (networkError != NetworkError.Ok)
            {
                Debug.Log("NetworkTransport.Receive failed: hostid=" + clientId + " connId=" + connectionId + " channelId=" + channel + " error=" + networkError);
            }

            switch (networkEvent)
            {
                case NetworkEventType.ConnectEvent:
                    transportEvent = TransportEvent.Connected;
                    break;
                case NetworkEventType.DataEvent:
                    transportEvent = TransportEvent.Data;
                    data = new byte[receivedSize];
                    Array.Copy(clientReceiveBuffer, data, receivedSize);
                    break;
                case NetworkEventType.DisconnectEvent:
                    transportEvent = TransportEvent.Disconnected;
                    break;
                default:
                    return false;
            }

            return true;
        }

        public override void ClientDisconnect()
        {
            if (clientId != -1)
            {
                NetworkTransport.RemoveHost(clientId);
                clientId = -1;
            }
        }

        // server //////////////////////////////////////////////////////////////
        public override bool ServerActive()
        {
            return serverHostId != -1;
        }

        public override void ServerStart()
        {
            if (useWebsockets)
            {
                HostTopology topology = new HostTopology(connectionConfig, int.MaxValue);
                serverHostId = NetworkTransport.AddWebsocketHost(topology, port);
                //Debug.Log("LLAPITransport.ServerStartWebsockets port=" + port + " max=" + maxConnections + " hostid=" + serverHostId);
            }
            else
            {
                HostTopology topology = new HostTopology(connectionConfig, int.MaxValue);
                serverHostId = NetworkTransport.AddHost(topology, port);
                //Debug.Log("LLAPITransport.ServerStart port=" + port + " max=" + maxConnections + " hostid=" + serverHostId);
            }
        }

        public override bool ServerSend(int connectionId, int channelId, byte[] data)
        {
            return NetworkTransport.Send(serverHostId, connectionId, channelId, data, data.Length, out error);
        }

        public override bool ServerGetNextMessage(out int connectionId, out TransportEvent transportEvent, out byte[] data)
        {
            connectionId = -1;
            transportEvent = TransportEvent.Disconnected;
            data = null;
            int channel;
            int receivedSize;
            NetworkEventType networkEvent = NetworkTransport.ReceiveFromHost(serverHostId, out connectionId, out channel, serverReceiveBuffer, serverReceiveBuffer.Length, out receivedSize, out error);

            // note: 'error' is used for extra information, e.g. the reason for
            // a disconnect. we don't necessarily have to throw an error if
            // error != 0. but let's log it for easier debugging.
            //
            // DO NOT return after error != 0. otherwise Disconnect won't be
            // registered.
            NetworkError networkError = (NetworkError)error;
            if (networkError != NetworkError.Ok)
            {
                Debug.Log("NetworkTransport.Receive failed: hostid=" + serverHostId + " connId=" + connectionId + " channelId=" + channel + " error=" + networkError);
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
                    transportEvent = TransportEvent.Connected;
                    break;
                case NetworkEventType.DataEvent:
                    transportEvent = TransportEvent.Data;
                    data = new byte[receivedSize];
                    Array.Copy(serverReceiveBuffer, data, receivedSize);
                    break;
                case NetworkEventType.DisconnectEvent:
                    transportEvent = TransportEvent.Disconnected;
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

        public override bool GetConnectionInfo(int connectionId, out string address)
        {
            int port;
            NetworkID networkId;
            NodeID node;
            NetworkTransport.GetConnectionInfo(serverHostId, connectionId, out address, out port, out networkId, out node, out error);
            return true;
        }

        public override void ServerStop()
        {
            NetworkTransport.RemoveHost(serverHostId);
            serverHostId = -1;
            Debug.Log("LLAPITransport.ServerStop");
        }

        // common //////////////////////////////////////////////////////////////
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
                string ip;
                GetConnectionInfo(clientId, out ip);
                return "LLAPI Client ip: " + ip + " port: " + port;
            }
            return "LLAPI (inactive/disconnected)";
        }
    }
}