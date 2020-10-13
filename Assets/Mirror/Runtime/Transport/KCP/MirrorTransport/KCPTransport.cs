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
        // connections <connectionId, connection> where connectionId is EndPoint.GetHashCode
        Dictionary<int, KcpServerConnection> connections = new Dictionary<int, KcpServerConnection>();

        // client
        KcpClientConnection clientConnection;

        void Awake()
        {
            Debug.Log("KcpTransport initialized!");
        }

        // all except WebGL
        public override bool Available() =>
            Application.platform != RuntimePlatform.WebGLPlayer;

        // client
        // TODO connected only after OnConnected was called
        public override bool ClientConnected() => clientConnection != null;
        public override void ClientConnect(string address)
        {
            if (clientConnection != null)
            {
                Debug.LogWarning("KCP: client already connected!");
                return;
            }

            clientConnection = new KcpClientConnection();
            // setup events
            clientConnection.OnConnected += () =>
            {
                Debug.LogWarning($"KCP->Mirror OnClientConnected");
                OnClientConnected.Invoke();
            };
            clientConnection.OnData += (message) =>
            {
                Debug.LogWarning($"KCP->Mirror OnClientData({BitConverter.ToString(message.Array, message.Offset, message.Count)})");
                OnClientDataReceived.Invoke(message);
            };
            clientConnection.OnDisconnected += () =>
            {
                Debug.LogWarning($"KCP->Mirror OnClientDisconnected");
                OnClientDisconnected.Invoke();
            };

            // connect
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
                if (!connections.TryGetValue(connectionId, out KcpServerConnection connection))
                {
                    // add it to a queue
                    connection = new KcpServerConnection(serverSocket, serverNewClientEP);
                    //acceptedConnections.Writer.TryWrite(connection);
                    connections.Add(connectionId, connection);
                    Debug.LogWarning($"KCP: server added connection {serverNewClientEP}");

                    // setup connected event
                    connection.OnConnected += () =>
                    {
                        // call mirror event
                        Debug.LogWarning($"KCP->Mirror OnServerConnected({connectionId})");
                        OnServerConnected.Invoke(connectionId);
                    };

                    // setup data event
                    connection.OnData += (message) =>
                    {
                        // call mirror event
                        Debug.LogWarning($"KCP->Mirror OnServerDataReceived({connectionId}, {BitConverter.ToString(message.Array, message.Offset, message.Count)})");
                        OnServerDataReceived.Invoke(connectionId, message);
                    };

                    // setup disconnected event
                    connection.OnDisconnected += () =>
                    {
                        // remove from connections
                        connections.Remove(connectionId);

                        // call mirror event
                        Debug.LogWarning($"KCP->Mirror OnServerDisconnected({connectionId})");
                        OnServerDisconnected.Invoke(connectionId);
                    };

                    // send handshake
                    connection.Handshake();
                }

                connection.RawInput(buffer, msgLength);
            }

            // tick all server connections
            foreach (KcpServerConnection connection in connections.Values)
            {
                connection.Tick();
                connection.Receive();
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
            if (connections.TryGetValue(connectionId, out KcpServerConnection connection))
            {
                connection.Send(segment);
                return true;
            }
            return false;
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
