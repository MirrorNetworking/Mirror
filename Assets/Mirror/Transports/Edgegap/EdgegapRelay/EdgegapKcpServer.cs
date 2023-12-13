using System;
using System.Net;
using System.Net.Sockets;
using Mirror;
using UnityEngine;
using kcp2k;

namespace Edgegap
{
    public class EdgegapKcpServer : KcpServer
    {
        // need buffer larger than KcpClient.rawReceiveBuffer to add metadata
        readonly byte[] relayReceiveBuffer;

        // authentication
        public uint userId;
        public uint sessionId;
        public ConnectionState state = ConnectionState.Disconnected;

        // server is an UDP client talking to relay
        protected Socket relaySocket;
        public EndPoint remoteEndPoint;

        // ping
        double lastPingTime;

        // custom 'active'. while connected to relay
        bool relayActive;

        public EdgegapKcpServer(
            Action<int> OnConnected,
            Action<int, ArraySegment<byte>, KcpChannel> OnData,
            Action<int> OnDisconnected,
            Action<int, ErrorCode, string> OnError,
            KcpConfig config)
              // TODO don't call base. don't listen to local UdpServer at all?
              : base(OnConnected, OnData, OnDisconnected, OnError, config)
        {
            relayReceiveBuffer = new byte[config.Mtu + Protocol.Overhead];
        }

        public override bool IsActive() => relayActive;

        // custom start function with relay parameters; connects udp client.
        public void Start(string relayAddress, ushort relayPort, uint userId, uint sessionId)
        {
            // reset last state
            state = ConnectionState.Checking;
            this.userId = userId;
            this.sessionId = sessionId;

            // try resolve host name
            if (!Common.ResolveHostname(relayAddress, out IPAddress[] addresses))
            {
                OnError(0, ErrorCode.DnsResolve, $"Failed to resolve host: {relayAddress}");
                return;
            }

            // create socket
            remoteEndPoint = new IPEndPoint(addresses[0], relayPort);
            relaySocket = new Socket(remoteEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            relaySocket.Blocking = false;

            // configure buffer sizes
            Common.ConfigureSocketBuffers(relaySocket, config.RecvBufferSize, config.SendBufferSize);

            // bind to endpoint for Send/Receive instead of SendTo/ReceiveFrom
            relaySocket.Connect(remoteEndPoint);
            relayActive = true;
        }

        public override void Stop()
        {
            relayActive = false;
        }

        protected override bool RawReceiveFrom(out ArraySegment<byte> segment, out int connectionId)
        {
            segment = default;
            connectionId = 0;

            if (relaySocket == null) return false;

            try
            {
                // TODO need separate buffer. don't write into result yet. only payload

                if (relaySocket.ReceiveNonBlocking(relayReceiveBuffer, out ArraySegment<byte> content))
                {
                    using (NetworkReaderPooled reader = NetworkReaderPool.Get(content))
                    {
                        // parse message type
                        if (reader.Remaining == 0)
                        {
                            Debug.LogWarning($"EdgegapServer: message of {content.Count} is too small to parse header.");
                            return false;
                        }
                        byte messageType = reader.ReadByte();

                        // handle message type
                        switch (messageType)
                        {
                            case (byte)MessageType.Ping:
                            {
                                // parse state
                                if (reader.Remaining < 1) return false;
                                ConnectionState last = state;
                                state = (ConnectionState)reader.ReadByte();

                                // log state changes for debugging.
                                if (state != last) Debug.Log($"EdgegapServer: state updated to: {state}");

                                // return true indicates Mirror to keep checking
                                // for further messages.
                                return true;
                            }
                            case (byte)MessageType.Data:
                            {
                                // parse connectionId and payload
                                if (reader.Remaining <= 4)
                                {
                                    Debug.LogWarning($"EdgegapServer: message of {content.Count} is too small to parse connId.");
                                    return false;
                                }

                                connectionId = reader.ReadInt();
                                segment = reader.ReadBytesSegment(reader.Remaining);
                                // Debug.Log($"EdgegapServer: receiving from connId={connectionId}: {segment.ToHexString()}");
                                return true;
                            }
                            // wrong message type. return false, don't throw.
                            default: return false;
                        }
                    }
                }
            }
            catch (SocketException e)
            {
                Log.Info($"EdgegapServer: looks like the other end has closed the connection. This is fine: {e}");
            }
            return false;
        }

        protected override void RawSend(int connectionId, ArraySegment<byte> data)
        {
            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                // Debug.Log($"EdgegapServer: sending to connId={connectionId}: {data.ToHexString()}");
                writer.WriteUInt(userId);
                writer.WriteUInt(sessionId);
                writer.WriteByte((byte)MessageType.Data);
                writer.WriteInt(connectionId);
                writer.WriteBytes(data.Array, data.Offset, data.Count);
                ArraySegment<byte> message = writer;

                try
                {
                    relaySocket.SendNonBlocking(message);
                }
                catch (SocketException e)
                {
                    Log.Error($"KcpRleayServer: RawSend failed: {e}");
                }
            }
        }

        void SendPing()
        {
            using (NetworkWriterPooled writer = NetworkWriterPool.Get())
            {
                writer.WriteUInt(userId);
                writer.WriteUInt(sessionId);
                writer.WriteByte((byte)MessageType.Ping);
                ArraySegment<byte> message = writer;

                try
                {
                    relaySocket.SendNonBlocking(message);
                }
                catch (SocketException e)
                {
                    Debug.LogWarning($"EdgegapServer: failed to ping. perhaps the relay isn't running? {e}");
                }
            }
        }

        public override void TickOutgoing()
        {
            if (relayActive)
            {
                // ping every interval for keepalive & handshake
                if (NetworkTime.localTime >= lastPingTime + Protocol.PingInterval)
                {
                    SendPing();
                    lastPingTime = NetworkTime.localTime;
                }
            }

            // base processing
            base.TickOutgoing();
        }
    }
}
