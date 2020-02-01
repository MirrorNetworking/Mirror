using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System;
using System.Threading.Tasks;

// Based on https://github.com/EnlightenedOne/MirrorNetworkDiscovery
// forked from https://github.com/in0finite/MirrorNetworkDiscovery
// Both are MIT Licensed

namespace Mirror.Discovery
{
    /// <summary>
    /// Base implementation for Network Discovery.  Extend this component
    /// to provide custom discovery with game specific data
    /// <see cref="NetworkDiscovery"/> for a sample implementation
    /// </summary>
    [DisallowMultipleComponent]
    [HelpURL("https://mirror-networking.com/docs/Components/NetworkDiscovery.html")]
    public abstract class NetworkDiscoveryBase<Request, Response> : MonoBehaviour
        where Request : IMessageBase, new()
        where Response : IMessageBase, new()
    {
        public static bool SupportedOnThisPlatform { get { return Application.platform != RuntimePlatform.WebGLPlayer; } }

        // each game should have a random unique handshake,  this way you can tell if this is the same game or not
        [HideInInspector]
        public long secretHandshake;

        [SerializeField]
        [Tooltip("The UDP port the server will listen for multi-cast messages")]
        int serverBroadcastListenPort = 47777;

        [SerializeField]
        [Tooltip("Time in seconds between multi-cast messages")]
        [Range(1, 60)]
        float ActiveDiscoveryInterval = 3;

        protected UdpClient serverUdpClient = null;
        protected UdpClient clientUdpClient = null;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (secretHandshake == 0)
            {
                secretHandshake = RandomLong();
                UnityEditor.Undo.RecordObject(this, "Set secret handshake");
            }
        }
#endif

        public static long RandomLong()
        {
            int value1 = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            int value2 = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            return value1 + ((long)value2 << 32);
        }

        // Ensure the ports are cleared no matter when Game/Unity UI exits
        void OnApplicationQuit()
        {
            Shutdown();
        }

        void Shutdown()
        {
            if (serverUdpClient != null)
            {
                try
                {
                    serverUdpClient.Close();
                }
                catch (Exception)
                {
                    // it is just close, swallow the error
                }

                serverUdpClient = null;
            }

            if (clientUdpClient != null)
            {
                try
                {
                    clientUdpClient.Close();
                }
                catch (Exception)
                {
                    // it is just close, swallow the error
                }

                clientUdpClient = null;
            }

            CancelInvoke();
        }

        #region Server

        /// <summary>
        /// Advertise this server in the local network
        /// </summary>
        /// <param name="networkManager">Network Manager</param>
        public void AdvertiseServer()
        {
            if (!SupportedOnThisPlatform)
                throw new PlatformNotSupportedException("Network discovery not supported in this platform");

            StopDiscovery();

            // Setup port -- may throw exception
            serverUdpClient = new UdpClient(serverBroadcastListenPort)
            {
                EnableBroadcast = true,
                MulticastLoopback = false
            };

            // listen for client pings
            _ = ServerListenAsync();
        }

        public async Task ServerListenAsync()
        {
            while (true)
            {
                try
                {
                    await ReceiveRequestAsync(serverUdpClient);
                }
                catch (ObjectDisposedException)
                {
                    // socket has been closed
                    break;
                }
                catch (Exception)
                {
                    continue;
                }
            }
        }

        async Task ReceiveRequestAsync(UdpClient udpClient)
        {
            // only proceed if there is available data in network buffer, or otherwise Receive() will block
            // average time for UdpClient.Available : 10 us

            UdpReceiveResult udpReceiveResult = await udpClient.ReceiveAsync();

            NetworkReader networkReader = NetworkReaderPool.GetReader(udpReceiveResult.Buffer);

            long handshake = networkReader.ReadInt64();
            if (handshake != secretHandshake)
            {
                // message is not for us
                NetworkReaderPool.Recycle(networkReader);
                throw new ProtocolViolationException("Invalid handshake");
            }

            Request request = new Request();
            request.Deserialize(networkReader);

            ProcessClientRequest(request, udpReceiveResult.RemoteEndPoint);

            NetworkReaderPool.Recycle(networkReader);
        }

        /// <summary>
        /// Reply to the client to inform it of this server
        /// </summary>
        /// <remarks>
        /// Override if you wish to ignore server requests based on
        /// custom criteria such as language, full server game mode or difficulty
        /// </remarks>
        /// <param name="request">Request comming from client</param>
        /// <param name="endpoint">Address of the client that sent the request</param>
        protected virtual void ProcessClientRequest(Request request, IPEndPoint endpoint)
        {
            Response info = ProcessRequest(request, endpoint);

            if (info == null)
                return;

            NetworkWriter writer = NetworkWriterPool.GetWriter();

            try
            {
                writer.WriteInt64(secretHandshake);

                info.Serialize(writer);

                ArraySegment<byte> data = writer.ToArraySegment();
                // signature matches
                // send response
                serverUdpClient.Send(data.Array, data.Count, endpoint);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }
            finally
            {
                NetworkWriterPool.Recycle(writer);
            }
        }

        /// <summary>
        /// Process the request from a client
        /// </summary>
        /// <remarks>
        /// Override if you wish to provide more information to the clients
        /// such as the name of the host player
        /// </remarks>
        /// <param name="request">Request comming from client</param>
        /// <param name="endpoint">Address of the client that sent the request</param>
        /// <returns>The message to be sent back to the client or null</returns>
        protected abstract Response ProcessRequest(Request request, IPEndPoint endpoint);

        #endregion

        #region Client

        /// <summary>
        /// Start Active Discovery
        /// </summary>
        public void StartDiscovery()
        {
            if (!SupportedOnThisPlatform)
                throw new PlatformNotSupportedException("Network discovery not supported in this platform");

            StopDiscovery();

            try
            {
                // Setup port
                clientUdpClient = new UdpClient(0)
                {
                    EnableBroadcast = true,
                    MulticastLoopback = false
                };
            }
            catch (Exception)
            {
                // Free the port if we took it
                Shutdown();
                throw;
            }

            _ = ClientListenAsync();

            InvokeRepeating(nameof(BroadcastDiscoveryRequest), 0, ActiveDiscoveryInterval);
        }

        /// <summary>
        /// Start Active Discovery
        /// </summary>
        public void StopDiscovery()
        {
            Shutdown();
        }

        /// <summary>
        /// Awaits for server response
        /// </summary>
        /// <returns>ClientListenAsync Task</returns>
        public async Task ClientListenAsync()
        {
            while (true)
            {
                try
                {
                    await ReceiveGameBroadcastAsync(clientUdpClient);
                }
                catch (ObjectDisposedException)
                {
                    // socket was closed, no problem
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        /// <summary>
        /// Sends discovery request from client
        /// </summary>
        public void BroadcastDiscoveryRequest()
        {
            if (clientUdpClient == null)
                return;

            IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, serverBroadcastListenPort);

            NetworkWriter writer = NetworkWriterPool.GetWriter();

            writer.WriteInt64(secretHandshake);

            try
            {
                Request request = GetRequest();

                request.Serialize(writer);

                ArraySegment<byte> data = writer.ToArraySegment();

                clientUdpClient.SendAsync(data.Array, data.Count, endPoint);
            }
            catch (Exception)
            {
                // It is ok if we can't broadcast to one of the addresses
            }
            finally
            {
                NetworkWriterPool.Recycle(writer);
            }
        }

        /// <summary>
        /// Create a message that will be broadcasted on the network to discover servers
        /// </summary>
        /// <remarks>
        /// Override if you wish to include additional data in the discovery message
        /// such as desired game mode, language, difficulty, etc... </remarks>
        /// <returns>An instance of ServerRequest with data to be broadcasted</returns>
        protected virtual Request GetRequest() => new Request();

        async Task ReceiveGameBroadcastAsync(UdpClient udpClient)
        {
            // only proceed if there is available data in network buffer, or otherwise Receive() will block
            // average time for UdpClient.Available : 10 us

            UdpReceiveResult udpReceiveResult = await udpClient.ReceiveAsync();

            NetworkReader networkReader = NetworkReaderPool.GetReader(udpReceiveResult.Buffer);

            if (networkReader.ReadInt64() != secretHandshake)
            {
                NetworkReaderPool.Recycle(networkReader);
                return;
            }

            Response response = new Response();
            response.Deserialize(networkReader);

            ProcessResponse(response, udpReceiveResult.RemoteEndPoint);

            NetworkReaderPool.Recycle(networkReader);
        }

        /// <summary>
        /// Process the answer from a server
        /// </summary>
        /// <remarks>
        /// A client receives a reply from a server, this method processes the
        /// reply and raises an event
        /// </remarks>
        /// <param name="response">Response that came from the server</param>
        /// <param name="endpoint">Address of the server that replied</param>
        protected abstract void ProcessResponse(Response response, IPEndPoint endpoint);

        #endregion
    }
}
