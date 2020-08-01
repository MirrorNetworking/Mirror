using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Mirror
{

    [AddComponentMenu("Network/NetworkManager")]
    [HelpURL("https://mirror-networking.com/docs/Components/NetworkManager.html")]
    [RequireComponent(typeof(NetworkServer))]
    [RequireComponent(typeof(NetworkClient))]
    [DisallowMultipleComponent]
    public class NetworkManager : MonoBehaviour, INetworkManager
    {
        static readonly ILogger logger = LogFactory.GetLogger<NetworkManager>();

        public NetworkServer server;
        public NetworkClient client;

        /// <summary>
        /// True if the server or client is started and running
        /// <para>This is set True in StartServer / StartClient, and set False in StopServer / StopClient</para>
        /// </summary>
        public bool IsNetworkActive => server.Active || client.Active;

        /// <summary>
        /// This is invoked when a host is started.
        /// <para>StartHost has multiple signatures, but they all cause this hook to be called.</para>
        /// </summary>
        public UnityEvent OnStartHost = new UnityEvent();

        /// <summary>
        /// This is called when a host is stopped.
        /// </summary>
        public UnityEvent OnStopHost = new UnityEvent();

        // full server setup code, without spawning objects yet
        async Task SetupServer()
        {
            logger.Log("NetworkManager SetupServer");

            // start listening to network connections
            await server.ListenAsync();
        }

        /// <summary>
        /// This starts a new server.
        /// </summary>
        /// <returns></returns>
        public async Task StartServer()
        {
            await SetupServer();
        }

        /// <summary>
        /// This starts a network client. It uses the networkAddress property as the address to connect to.
        /// <para>This makes the newly created client connect to the server immediately.</para>
        /// </summary>
        public Task StartClient(string serverIp)
        {
            if (logger.LogEnabled()) logger.Log("NetworkManager StartClient address:" + serverIp);

            var builder = new UriBuilder
            {
                Host = serverIp,
                Scheme = client.Transport.Scheme.First(),
            };

            return client.ConnectAsync(builder.Uri);
        }

        /// <summary>
        /// This starts a network client. It uses the Uri parameter as the address to connect to.
        /// <para>This makes the newly created client connect to the server immediately.</para>
        /// </summary>
        /// <param name="uri">location of the server to connect to</param>
        public void StartClient(Uri uri)
        {
            if (logger.LogEnabled()) logger.Log("NetworkManager StartClient address:" + uri);

            _ = client.ConnectAsync(uri);
        }

        /// <summary>
        /// This starts a network "host" - a server and client in the same application.
        /// <para>The client returned from StartHost() is a special "local" client that communicates to the in-process server using a message queue instead of the real network. But in almost all other cases, it can be treated as a normal client.</para>
        /// </summary>
        public async Task StartHost()
        {
            // setup server first
            await SetupServer();

            client.ConnectHost(server);

            // call OnStartHost AFTER SetupServer. this way we can use
            // NetworkServer.Spawn etc. in there too. just like OnStartServer
            // is called after the server is actually properly started.
            OnStartHost.Invoke();

            logger.Log("NetworkManager StartHost");
        }

        /// <summary>
        /// This stops both the client and the server that the manager is using.
        /// </summary>
        public void StopHost()
        {
            OnStopHost.Invoke();
            StopClient();
            StopServer();
        }

        /// <summary>
        /// Stops the server that the manager is using.
        /// </summary>
        public void StopServer()
        {
            server.Disconnect();
        }

        /// <summary>
        /// Stops the client that the manager is using.
        /// </summary>
        public void StopClient()
        {
            client.Disconnect();
        }
    }
}
