using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

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

        /// <summary>
        /// Automatically invoke StartServer()
        /// <para>If the application is a Server Build or run with the -batchMode ServerRpc line arguement, StartServer is automatically invoked.</para>
        /// </summary>
        [Tooltip("Should the server auto-start when the game is started in a headless build?")]
        public bool startOnHeadless = true;

        /// <summary>
        /// Server Update frequency, per second. Use around 60Hz for fast paced games like Counter-Strike to minimize latency. Use around 30Hz for games like WoW to minimize computations. Use around 1-10Hz for slow paced games like EVE.
        /// </summary>
        [Tooltip("Server Update frequency, per second. Use around 60Hz for fast paced games like Counter-Strike to minimize latency. Use around 30Hz for games like WoW to minimize computations. Use around 1-10Hz for slow paced games like EVE.")]
        public int serverTickRate = 30;

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

        #region Unity Callbacks

        /// <summary>
        /// virtual so that inheriting classes' Start() can call base.Start() too
        /// </summary>
        public virtual void Start()
        {
            logger.Log("Thank you for using Mirror! https://mirror-networking.com");

            // headless mode? then start the server
            // can't do this in Awake because Awake is for initialization.
            // some transports might not be ready until Start.
            //
            // (tick rate is applied in StartServer!)
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null && startOnHeadless)
            {
                _ = StartServer();
            }
        }

        #endregion

        #region Start & Stop

        // full server setup code, without spawning objects yet
        async Task SetupServer()
        {
            logger.Log("NetworkManager SetupServer");

            ConfigureServerFrameRate();

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

            FinishStartHost();
        }

        // FinishStartHost is guaranteed to be called after the host server was
        // fully started and all the asynchronous StartHost magic is finished
        // (= scene loading), or immediately if there was no asynchronous magic.
        //
        // note: we don't really need FinishStartClient/FinishStartServer. the
        //       host version is enough.
        void FinishStartHost()
        {
            // connect client and call OnStartClient AFTER server scene was
            // loaded and all objects were spawned.
            // DO NOT do this earlier. it would cause race conditions where a
            // client will do things before the server is even fully started.
            logger.Log("StartHostClient called");
            StartHostClient();
        }

        void StartHostClient()
        {
            logger.Log("NetworkManager ConnectLocalClient");

            server.ActivateHostScene();
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

        /// <summary>
        /// Set the frame rate for a headless server.
        /// <para>Override if you wish to disable the behavior or set your own tick rate.</para>
        /// </summary>
        public virtual void ConfigureServerFrameRate()
        {
            // set a fixed tick rate instead of updating as often as possible
            // * if not in Editor (it doesn't work in the Editor)
            // * if not in Host mode
#if !UNITY_EDITOR
            if (!client.Active && SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Application.targetFrameRate = serverTickRate;
                if (logger.logEnabled) logger.Log("Server Tick Rate set to: " + Application.targetFrameRate + " Hz.");
            }
#endif
        }

        /// <summary>
        /// virtual so that inheriting classes' OnDestroy() can call base.OnDestroy() too
        /// </summary>
        public virtual void OnDestroy()
        {
            logger.Log("NetworkManager destroyed");
        }

        #endregion

    }
}
