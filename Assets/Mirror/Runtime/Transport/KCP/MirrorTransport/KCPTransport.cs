using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Mirror.KCP
{
    public class KCPTransport : Transport
    {
        // common
        [Header("Transport Configuration")]
        public ushort Port = 7777;
        readonly byte[] buffer = new byte[Kcp.MTU_DEF];

        // server
        Socket serverSocket;
        EndPoint serverNewClientEP = new IPEndPoint(IPAddress.IPv6Any, 0);
        // connections <connectionId, connection> where connectionId is EndPoint.GetHashCode
        Dictionary<int, KcpServerConnection> connections = new Dictionary<int, KcpServerConnection>();

        // a queue of all the connections that need to be ticked
        // sorted by the time to check
        readonly PriorityQueue<long, KcpServerConnection> tickQueue = new PriorityQueue<long, KcpServerConnection>();
        readonly Stopwatch timer = new Stopwatch();

        // client
        KcpClientConnection clientConnection;

        void Awake()
        {
            Debug.Log("KcpTransport initialized!");
            timer.Start();
        }

        // all except WebGL
        public override bool Available() =>
            Application.platform != RuntimePlatform.WebGLPlayer;

        // use same Kcp configuration on server and client
        void ConfigureKcpConnection(KcpConnection connection)
        {
            // NoDelay=false doesn't scale past ~1000 monsters. let's force enable it.
            connection.kcp.SetNoDelay(true, 10, 2, true);
            // PUMP those numbers up.
            // this works for 4k:
            //connection.kcp.SetWindowSize(128, 128);
            // this works for 10k:
            //connection.kcp.SetWindowSize(512, 512);
            // this works for 20k:
            connection.kcp.SetWindowSize(8192, 8192);
        }

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
                //Debug.LogWarning($"KCP->Mirror OnClientData({BitConverter.ToString(message.Array, message.Offset, message.Count)})");
                OnClientDataReceived.Invoke(message);
            };
            clientConnection.OnDisconnected += () =>
            {
                Debug.LogWarning($"KCP->Mirror OnClientDisconnected");
                OnClientDisconnected.Invoke();
            };

            // connect
            clientConnection.Connect(address, Port);

            // configure connection for max scale
            ConfigureKcpConnection(clientConnection);
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
                //Debug.Log($"KCP: server raw recv {msgLength} bytes = {BitConverter.ToString(buffer, 0, msgLength)}");

                // calculate connectionId from endpoint
                int connectionId = serverNewClientEP.GetHashCode();

                // is this a new connection?
                if (!connections.TryGetValue(connectionId, out KcpServerConnection connection))
                {
                    // add it to a queue
                    connection = new KcpServerConnection(serverSocket, serverNewClientEP);

                    // configure connection for max scale
                    ConfigureKcpConnection(connection);

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
                        //Debug.LogWarning($"KCP->Mirror OnServerDataReceived({connectionId}, {BitConverter.ToString(message.Array, message.Offset, message.Count)})");
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

                    // queue up the connection for ticking
                    tickQueue.Enqueue(timer.ElapsedMilliseconds, connection);

                    // send handshake
                    connection.Handshake();
                }

                connection.RawInput(buffer, msgLength);
                connection.Receive();
            }

            TickConnections();
        }

        private void TickConnections()
        {
            if (tickQueue.Count <= 0)
                return;

            long now = timer.ElapsedMilliseconds;

            (long timecheck, KcpServerConnection connection) = tickQueue.Peek();

            while (timecheck <= now)
            {
                tickQueue.Dequeue();

                int connectionId = connection.remoteEndpoint.GetHashCode();
                if (connections.ContainsKey(connectionId)) {
                    connection.Tick();
                    connection.Receive();
                    tickQueue.Enqueue(now + connection.NextTick(), connection);
                }

                (timecheck, connection) = tickQueue.Peek();
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

        int GetTotalSendQueue() =>
            connections.Values.Sum(conn => conn.kcp.sendQueue.Count);
        int GetTotalReceiveQueue() =>
            connections.Values.Sum(conn => conn.kcp.receiveQueue.Count);
        int GetTotalSendBuffer() =>
            connections.Values.Sum(conn => conn.kcp.sendBuffer.Count);
        int GetTotalReceiveBuffer() =>
            connections.Values.Sum(conn => conn.kcp.sendBuffer.Count);

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(5, 100, 300, 300));

            if (ServerActive())
            {
                GUILayout.BeginVertical("Box");
                GUILayout.Label("SERVER");
                GUILayout.Label("  SendQueue: " + GetTotalSendQueue());
                GUILayout.Label("  ReceiveQueue: " + GetTotalReceiveQueue());
                GUILayout.Label("  SendQBuffer: " + GetTotalSendBuffer());
                GUILayout.Label("  ReceiveQBuffer: " + GetTotalReceiveBuffer());
                GUILayout.EndVertical();
            }

            if (ClientConnected())
            {
                GUILayout.BeginVertical("Box");
                GUILayout.Label("CLIENT");
                GUILayout.Label("  SendQueue: " + clientConnection.kcp.sendQueue.Count);
                GUILayout.Label("  ReceiveQueue: " + clientConnection.kcp.receiveQueue.Count);
                GUILayout.Label("  SendQBuffer: " + clientConnection.kcp.sendBuffer.Count);
                GUILayout.Label("  ReceiveQBuffer: " + clientConnection.kcp.receiveBuffer.Count);
                GUILayout.EndVertical();
            }

            GUILayout.EndArea();
        }
    }
}
