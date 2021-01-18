using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Mirror
{
    [Serializable] public class NetworkConnectionEvent : UnityEvent<INetworkConnection> { }

    public enum ConnectState
    {
        Disconnected,
        Connecting,
        Connected,
    }

    /// <summary>
    /// This is a network client class used by the networking system. It contains a NetworkConnection that is used to connect to a network server.
    /// <para>The <see cref="NetworkClient">NetworkClient</see> handle connection state, messages handlers, and connection configuration. There can be many <see cref="NetworkClient">NetworkClient</see> instances in a process at a time, but only one that is connected to a game server (<see cref="NetworkServer">NetworkServer</see>) that uses spawned objects.</para>
    /// <para><see cref="NetworkClient">NetworkClient</see> has an internal update function where it handles events from the transport layer. This includes asynchronous connect events, disconnect events and incoming data from a server.</para>
    /// </summary>
    [AddComponentMenu("Network/NetworkClient")]
    [DisallowMultipleComponent]
    public class NetworkClient : MonoBehaviour, INetworkClient
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(NetworkClient));

        [Header("Authentication")]
        [Tooltip("Authentication component attached to this object")]
        public NetworkAuthenticator authenticator;

        internal readonly Dictionary<uint, NetworkIdentity> spawned = new Dictionary<uint, NetworkIdentity>();

        /// <summary>
        /// Event fires once the Client has connected its Server.
        /// </summary>
        public NetworkConnectionEvent Connected = new NetworkConnectionEvent();

        /// <summary>
        /// Event fires after the Client connection has sucessfully been authenticated with its Server.
        /// </summary>
        public NetworkConnectionEvent Authenticated = new NetworkConnectionEvent();

        /// <summary>
        /// Event fires after the Client has disconnected from its Server and Cleanup has been called.
        /// </summary>
        public UnityEvent Disconnected = new UnityEvent();

        /// <summary>
        /// The NetworkConnection object this client is using.
        /// </summary>
        public INetworkConnection Connection { get; internal set; }

        /// <summary>
        /// NetworkIdentity of the localPlayer
        /// </summary>
        public NetworkIdentity LocalPlayer => Connection?.Identity;

        internal ConnectState connectState = ConnectState.Disconnected;

        /// <summary>
        /// active is true while a client is connecting/connected
        /// (= while the network is active)
        /// </summary>
        public bool Active => connectState == ConnectState.Connecting || connectState == ConnectState.Connected;

        /// <summary>
        /// This gives the current connection status of the client.
        /// </summary>
        public bool IsConnected => connectState == ConnectState.Connected;

        public readonly NetworkTime Time = new NetworkTime();

        public Transport Transport;

        /// <summary>
        /// List of all objects spawned in this client
        /// </summary>
        public Dictionary<uint, NetworkIdentity> Spawned
        {
            get
            {
                // if we are in host mode,  the list of spawned object is the same as the server list
                if (hostServer != null)
                    return hostServer.Spawned;
                else
                    return spawned;
            }
        }

        /// <summary>
        /// The host server
        /// </summary>
        internal NetworkServer hostServer;

        /// <summary>
        /// NetworkClient can connect to local server in host mode too
        /// </summary>
        public bool IsLocalClient => hostServer != null;

        /// <summary>
        /// Connect client to a NetworkServer instance.
        /// </summary>
        /// <param name="serverIp">Address of the server to connect to</param>
        public UniTask ConnectAsync(string serverIp)
        {
            if (logger.LogEnabled()) logger.Log("Client address:" + serverIp);

            var builder = new UriBuilder
            {
                Host = serverIp,
                Scheme = Transport.Scheme.First(),
            };

            return ConnectAsync(builder.Uri);
        }

        /// <summary>
        /// Connect client to a NetworkServer instance.
        /// </summary>
        /// <param name="serverIp">Address of the server to connect to</param>
        /// <param name="port">The port of the server to connect to</param>
        public UniTask ConnectAsync(string serverIp, ushort port)
        {
            if (logger.LogEnabled()) logger.Log("Client address and port:" + serverIp + ":" + port);

            var builder = new UriBuilder
            {
                Host = serverIp,
                Port = port,
                Scheme = Transport.Scheme.First()
            };

            return ConnectAsync(builder.Uri);
        }

        /// <summary>
        /// Connect client to a NetworkServer instance.
        /// </summary>
        /// <param name="uri">Address of the server to connect to</param>
        public async UniTask ConnectAsync(Uri uri)
        {
            if (logger.LogEnabled()) logger.Log("Client Connect: " + uri);

            if (Transport == null)
                Transport = GetComponent<Transport>();
            if (Transport == null)
                throw new InvalidOperationException("Transport could not be found for NetworkClient");

            connectState = ConnectState.Connecting;

            try
            {
                IConnection transportConnection = await Transport.ConnectAsync(uri);

                InitializeAuthEvents();

                // setup all the handlers
                Connection = GetNewConnection(transportConnection);
                Time.Reset();

                RegisterMessageHandlers();
                Time.UpdateClient(this);
                OnConnected().Forget();
            }
            catch (Exception)
            {
                connectState = ConnectState.Disconnected;
                throw;
            }
        }

        internal void ConnectHost(NetworkServer server)
        {
            logger.Log("Client Connect Host to Server");
            connectState = ConnectState.Connected;

            InitializeAuthEvents();

            // create local connection objects and connect them
            (IConnection c1, IConnection c2) = PipeConnection.CreatePipe();

            server.SetLocalConnection(this, c2);
            hostServer = server;
            Connection = GetNewConnection(c1);
            RegisterHostHandlers();

            OnConnected().Forget();
        }

        /// <summary>
        /// Creates a new INetworkConnection based on the provided IConnection.
        /// </summary>
        public virtual INetworkConnection GetNewConnection(IConnection connection)
        {
            return new NetworkConnection(connection);
        }

        void InitializeAuthEvents()
        {
            if (authenticator != null)
            {
                authenticator.OnClientAuthenticated += OnAuthenticated;

                Connected.AddListener(authenticator.OnClientAuthenticateInternal);
            }
            else
            {
                // if no authenticator, consider connection as authenticated
                Connected.AddListener(OnAuthenticated);
            }
        }

        async UniTaskVoid OnConnected()
        {
            // reset network time stats

            // the handler may want to send messages to the client
            // thus we should set the connected state before calling the handler
            connectState = ConnectState.Connected;
            Connected.Invoke(Connection);

            // start processing messages
            try
            {
                await Connection.ProcessMessagesAsync();
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
            }
            finally
            {
                Cleanup();

                Disconnected.Invoke();
            }
        }

        internal void OnAuthenticated(INetworkConnection conn)
        {
            Authenticated?.Invoke(conn);
        }

        /// <summary>
        /// Disconnect from server.
        /// <para>The disconnect message will be invoked.</para>
        /// </summary>
        public void Disconnect()
        {
            Connection?.Disconnect();
        }

        /// <summary>
        /// This sends a network message with a message Id to the server. This message is sent on channel zero, which by default is the reliable channel.
        /// <para>The message must be an instance of a class derived from MessageBase.</para>
        /// <para>The message id passed to Send() is used to identify the handler function to invoke on the server when the message is received.</para>
        /// </summary>
        /// <typeparam name="T">The message type to unregister.</typeparam>
        /// <param name="message"></param>
        /// <param name="channelId"></param>
        /// <returns>True if message was sent.</returns>
        public UniTask SendAsync<T>(T message, int channelId = Channel.Reliable)
        {
            return Connection.SendAsync(message, channelId);
        }

        public void Send<T>(T message, int channelId = Channel.Reliable)
        {
            Connection.SendAsync(message, channelId).Forget();
        }

        internal void Update()
        {
            // local connection?
            if (!IsLocalClient && Active && connectState == ConnectState.Connected)
            {
                // only update things while connected
                Time.UpdateClient(this);
            }
        }

        internal void RegisterHostHandlers()
        {
            Connection.RegisterHandler<NetworkPongMessage>(msg => { });
        }

        internal void RegisterMessageHandlers()
        {
            Connection.RegisterHandler<NetworkPongMessage>(Time.OnClientPong);
        }


        /// <summary>
        /// Shut down a client.
        /// <para>This should be done when a client is no longer going to be used.</para>
        /// </summary>
        void Cleanup()
        {
            logger.Log("Shutting down client.");

            hostServer = null;

            connectState = ConnectState.Disconnected;

            if (authenticator != null)
            {
                authenticator.OnClientAuthenticated -= OnAuthenticated;

                Connected.RemoveListener(authenticator.OnClientAuthenticateInternal);
            }
            else
            {
                // if no authenticator, consider connection as authenticated
                Connected.RemoveListener(OnAuthenticated);
            }
        }
    }
}
