using UnityEngine;
using System.Net;
using System;
using UnityEngine.Events;

namespace Mirror.Discovery
{
    // Based on https://github.com/EnlightenedOne/MirrorNetworkDiscovery
    // forked from https://github.com/in0finite/MirrorNetworkDiscovery
    // Both are MIT Licensed
    [Serializable]
    public class ServerFoundUnityEvent : UnityEvent<ServerResponse> { };

    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkDiscovery")]
    public class NetworkDiscovery : NetworkDiscoveryBase<ServerRequest, ServerResponse>
    {
        #region Server

        public long ServerId { get; private set; }

        [Tooltip("Transport used for discovery")]
        public Transport transport;

        [Tooltip("Event handler to invoke when a server is found")]
        public ServerFoundUnityEvent ServerFound;

        public void Start()
        {
            ServerId = RandomLong();

            // active transport gets initialized in awake
            // so make sure we set it here in Start()  (after awakes)
            // Or just let the user assign it in the inspector
            if (transport == null)
                transport = Transport.activeTransport;
        }

        /// <summary>
        /// Process the request from a client
        /// </summary>
        /// <remarks>
        /// Override if you wish to provide more information to the clients
        /// such as the name of the host player
        /// </remarks>
        /// <param name="_"></param>
        /// <param name="endpoint"></param>
        /// <returns>A message containing information about this server</returns>
        protected override ServerResponse ProcessRequest(ServerRequest _, IPEndPoint endpoint)
        {
            // In this case we don't do anything with the request
            // but other discovery implementations might want to use the data
            // in there,  This way the client can ask for
            // specific game mode or something

            // this is an example reply message,  return your own
            // to include whatever is relevant for your game
            return new ServerResponse
            {
                serverId = ServerId,
                uri = transport.ServerUri()
            };
        }

        #endregion

        #region Client

        /// <summary>
        /// Create a message that will be broadcasted on the network to discover servers
        /// </summary>
        /// <remarks>
        /// Override if you wish to include additional data in the discovery message
        /// such as desired game mode, language, difficulty, etc... </remarks>
        /// <returns>An instance of ServerRequest with data to be broadcasted</returns>
        protected override ServerRequest GetRequest() => new ServerRequest();

        /// <summary>
        /// Process the answer from a server
        /// </summary>
        /// <remarks>
        /// A client receives a reply from a server, this method processes the
        /// reply and raises an event
        /// </remarks>
        /// <param name="packet"></param>
        /// <param name="remoteEndPoint"></param>
        protected override void ProcessResponse(ServerResponse packet, IPEndPoint remoteEndPoint)
        {
            // we received a message from the remote endpoint
            packet.EndPoint = remoteEndPoint;

            // although we got a supposedly valid url, we may not be able to resolve
            // the provided host
            // However we know the real ip address of the server because we just
            // received a packet from it,  so use that as host.
            UriBuilder realUri = new UriBuilder(packet.uri)
            {
                Host = packet.EndPoint.Address.ToString()
            };
            packet.uri = realUri.Uri;

            ServerFound.Invoke(packet);
        }

        #endregion
    }
}
