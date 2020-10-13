using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Mirror.KCP
{
    public class KCPTransport : Transport
    {
        // common
        [Header("Transport Configuration")]
        public ushort Port = 7777;
        readonly byte[] buffer = new byte[1500];

        // server
        Socket serverSocket;
        EndPoint serverNewClientEP = new IPEndPoint(IPAddress.IPv6Any, 0);
        Dictionary<EndPoint, KcpServerConnection> connections = new Dictionary<EndPoint, KcpServerConnection>();

        // client
        KcpClientConnection clientConnection;
        bool clientReceivedAnything;

        void Awake()
        {
            Debug.Log("KcpTransport initialized!");
        }

        // all except WebGL
        public override bool Available() =>
            Application.platform != RuntimePlatform.WebGLPlayer;

        // client
        public override bool ClientConnected() =>
            clientConnection != null && clientReceivedAnything;
        public override void ClientConnect(string address)
        {
            if (clientConnection != null)
            {
                Debug.LogWarning("KCP: client already connected!");
                return;
            }

            // reset
            clientReceivedAnything = false;

            clientConnection = new KcpClientConnection();
            // setup events
            clientConnection.OnData += (message) =>
            {
                // first ever message?
                if (!clientReceivedAnything)
                {
                    clientReceivedAnything = true;
                    OnClientConnected.Invoke();
                }

                // message!
                OnClientDataReceived.Invoke(message);
            };
            clientConnection.OnDisconnected += () =>
            {
                OnClientDisconnected.Invoke();
            };
            clientConnection.Connect(address, Port);
        }
        public override bool ClientSend(int channelId, ArraySegment<byte> segment)
        {
            if (clientConnection != null)
            {
                clientConnection.Send(segment);
                return true;
            }
            Debug.LogWarning("KCP: can't send because client not connected!");
            return false;
        }

        public override void ClientDisconnect()
        {
            clientConnection?.Disconnect();
            clientConnection = null;
        }

        void UpdateServer()
        {
            while (serverSocket != null && serverSocket.Poll(0, SelectMode.SelectRead))
            {
                int msgLength = serverSocket.ReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref serverNewClientEP);
                Debug.Log($"KCP: server raw recv {msgLength} bytes = {BitConverter.ToString(buffer, 0, msgLength)}");

                // calculate connectionId from endpoint
                int connectionId = serverNewClientEP.GetHashCode();

                // is this a new connection?
                if (!connections.TryGetValue(serverNewClientEP, out KcpServerConnection connection))
                {
                    // add it to a queue
                    connection = new KcpServerConnection(serverSocket, serverNewClientEP);
                    //acceptedConnections.Writer.TryWrite(connection);
                    connections.Add(serverNewClientEP, connection);
                    Debug.LogWarning($"KCP: server added connection {serverNewClientEP}");
                    // setup data event
                    connection.OnData += (message) =>
                    {
                        // call mirror event
                        OnServerDataReceived.Invoke(connectionId, message);
                    };
                    // setup disconnected event
                    connection.OnDisconnected += () =>
                    {
                        // remove from connections
                        connections.Remove(serverNewClientEP);

                        // call mirror event
                        OnServerDisconnected.Invoke(connectionId);
                    };
                    // send handshake
                    connection.Handshake();

                    // call mirror event
                    OnServerConnected.Invoke(connectionId);
                }

                connection.RawInput(buffer, msgLength);
            }

            // tick all server connections
            foreach (KeyValuePair<EndPoint, KcpServerConnection> kvp in connections)
            {
                kvp.Value.Tick();
                kvp.Value.Receive();
            }
        }

        void UpdateClient()
        {
            // tick client connection
            if (clientConnection != null)
            {
                clientConnection.Tick();
                // recv on socket
                clientConnection.RawReceive();
                // recv on kcp
                clientConnection.Receive();
            }
        }

        // IMPORTANT: set script execution order to >1000 to call Transport's
        //            LateUpdate after all others. Fixes race condition where
        //            e.g. in uSurvival Transport would apply Cmds before
        //            ShoulderRotation.LateUpdate, resulting in projectile
        //            spawns at the point before shoulder rotation.
        public void LateUpdate()
        {
            // note: we need to check enabled in case we set it to false
            // when LateUpdate already started.
            // (https://github.com/vis2k/Mirror/pull/379)
            if (!enabled)
                return;

            UpdateServer();
            UpdateClient();
        }

        // server
        public override bool ServerActive() => serverSocket != null;
        public override void ServerStart()
        {
            // only start once
            if (serverSocket != null)
            {
                Debug.LogWarning("KCP: server already started!");
            }

            // listen
            serverSocket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
            serverSocket.DualMode = true;
            serverSocket.Bind(new IPEndPoint(IPAddress.IPv6Any, Port));

            // TODO accept
            //KcpServerConnection connection = await acceptedConnections.Reader.ReadAsync();
            //await connection.Handshake();
            //return connection;
        }
        public override bool ServerSend(int connectionId, int channelId, ArraySegment<byte> segment)
        {
            throw new NotImplementedException();
        }
        public override bool ServerDisconnect(int connectionId) => throw new NotImplementedException();
        public override string ServerGetClientAddress(int connectionId)
        {
            throw new NotImplementedException();
        }
        public override void ServerStop()
        {
            serverSocket?.Close();
            serverSocket = null;
        }

        // common
        public override void Shutdown()
        {
            throw new NotImplementedException();
        }

        // MTU
        public override ushort GetMaxPacketSize() => Kcp.MTU_DEF;

        public override string ToString()
        {
            return "KCP";
        }
    }
}
