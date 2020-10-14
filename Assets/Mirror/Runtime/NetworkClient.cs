using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using Guid = System.Guid;
using Object = UnityEngine.Object;

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
    /// <para>The <see cref="NetworkManager">NetworkManager</see> has a NetworkClient instance that it uses for games that it starts, but the NetworkClient may be used by itself.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class NetworkClient : MonoBehaviour, INetworkClient
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(NetworkClient));

        [Header("Authentication")]
        [Tooltip("Authentication component attached to this object")]
        public NetworkAuthenticator authenticator;

        // spawn handlers. internal for testing purposes. do not use directly.
        internal readonly Dictionary<Guid, SpawnHandlerDelegate> spawnHandlers = new Dictionary<Guid, SpawnHandlerDelegate>();
        internal readonly Dictionary<Guid, UnSpawnDelegate> unspawnHandlers = new Dictionary<Guid, UnSpawnDelegate>();
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

        /// <summary>
        /// List of prefabs that will be registered with the spawning system.
        /// <para>For each of these prefabs, ClientManager.RegisterPrefab() will be automatically invoke.</para>
        /// </summary>
        public List<GameObject> spawnPrefabs = new List<GameObject>();

        public readonly NetworkTime Time = new NetworkTime();

        public Transport Transport;

        /// <summary>
        /// This is a dictionary of the prefabs that are registered on the client with ClientScene.RegisterPrefab().
        /// <para>The key to the dictionary is the prefab asset Id.</para>
        /// </summary>
        private readonly Dictionary<Guid, GameObject> prefabs = new Dictionary<Guid, GameObject>();

        /// <summary>
        /// This is dictionary of the disabled NetworkIdentity objects in the scene that could be spawned by messages from the server.
        /// <para>The key to the dictionary is the NetworkIdentity sceneId.</para>
        /// </summary>
        public readonly Dictionary<ulong, NetworkIdentity> spawnableObjects = new Dictionary<ulong, NetworkIdentity>();

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
        public void ConnectAsync(string serverIp)
        {
            if (logger.LogEnabled()) logger.Log("Client address:" + serverIp);

            var builder = new UriBuilder
            {
                Host = serverIp,
                Scheme = Transport.Scheme.First(),
            };

            _ = ConnectAsync(builder.Uri);
        }
        /// <summary>
        /// Connect client to a NetworkServer instance.
        /// </summary>
        /// <param name="uri">Address of the server to connect to</param>
        public async UniTask ConnectAsync(Uri uri)
        {
            if (logger.LogEnabled()) logger.Log("Client Connect: " + uri);

            Transport transport = Transport;
            if (transport == null)
                transport = GetComponent<Transport>();

            connectState = ConnectState.Connecting;

            try
            {
                IConnection transportConnection = await transport.ConnectAsync(uri);

                RegisterSpawnPrefabs();
                InitializeAuthEvents();

                // setup all the handlers
                Connection = GetNewConnection(transportConnection);
                Time.Reset();

                RegisterMessageHandlers();
                Time.UpdateClient(this);
                _ = OnConnected();
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

            _ = OnConnected();
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

        /// <summary>
        /// client that received the message
        /// </summary>
        /// <remarks>This is a hack, but it is needed to deserialize
        /// gameobjects when processing the message</remarks>
        internal static NetworkClient Current { get; set; }

        async UniTask OnConnected()
        {
            // reset network time stats

            // the handler may want to send messages to the client
            // thus we should set the connected state before calling the handler
            connectState = ConnectState.Connected;
            Connected.Invoke(Connection);

            PrepareToSpawnSceneObjects();
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
        public UniTask SendAsync<T>(T message, int channelId = Channels.DefaultReliable)
        {
            return Connection.SendAsync(message, channelId);
        }

        public void Send<T>(T message, int channelId = Channels.DefaultReliable)
        {
            _ = Connection.SendAsync(message, channelId);
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
            Connection.RegisterHandler<ObjectDestroyMessage>(OnHostClientObjectDestroy);
            Connection.RegisterHandler<ObjectHideMessage>(OnHostClientObjectHide);
            Connection.RegisterHandler<NetworkPongMessage>(msg => { });
            Connection.RegisterHandler<SpawnMessage>(OnHostClientSpawn);
            // host mode reuses objects in the server
            // so we don't need to spawn them
            Connection.RegisterHandler<UpdateVarsMessage>(msg => { });
            Connection.RegisterHandler<RpcMessage>(OnRpcMessage);
        }

        internal void RegisterMessageHandlers()
        {
            Connection.RegisterHandler<ObjectDestroyMessage>(OnObjectDestroy);
            Connection.RegisterHandler<ObjectHideMessage>(OnObjectHide);
            Connection.RegisterHandler<NetworkPongMessage>(Time.OnClientPong);
            Connection.RegisterHandler<SpawnMessage>(OnSpawn);
            Connection.RegisterHandler<UpdateVarsMessage>(OnUpdateVarsMessage);
            Connection.RegisterHandler<RpcMessage>(OnRpcMessage);
        }

        /// <summary>
        /// Shut down a client.
        /// <para>This should be done when a client is no longer going to be used.</para>
        /// </summary>
        void Cleanup()
        {
            logger.Log("Shutting down client.");

            ClearSpawners();
            DestroyAllClientObjects();
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

        static bool ConsiderForSpawning(NetworkIdentity identity)
        {
            // not spawned yet, not hidden, etc.?
            return !identity.gameObject.activeSelf &&
                   identity.gameObject.hideFlags != HideFlags.NotEditable &&
                   identity.gameObject.hideFlags != HideFlags.HideAndDontSave &&
                   identity.sceneId != 0;
        }

        // this is called from message handler for Owner message
        internal void InternalAddPlayer(NetworkIdentity identity)
        {
            if (Connection != null)
            {
                Connection.Identity = identity;
            }
            else
            {
                logger.LogWarning("No ready connection found for setting player controller during InternalAddPlayer");
            }
        }

        /// <summary>
        /// Call this after loading/unloading a scene in the client after connection to register the spawnable objects
        /// </summary>
        public void PrepareToSpawnSceneObjects()
        {
            // add all unspawned NetworkIdentities to spawnable objects
            spawnableObjects.Clear();
            //TODO: This pulls all objects in all scenes. Find a way to pass the loaded scene and use Scene.GetRootGameObjects to be more efficient?
            IEnumerable<NetworkIdentity> sceneObjects =
                Resources.FindObjectsOfTypeAll<NetworkIdentity>()
                               .Where(ConsiderForSpawning);

            foreach (NetworkIdentity obj in sceneObjects)
            {
                spawnableObjects.Add(obj.sceneId, obj);
            }
        }

        #region Spawn Prefabs
        private void RegisterSpawnPrefabs()
        {
            for (int i = 0; i < spawnPrefabs.Count; i++)
            {
                GameObject prefab = spawnPrefabs[i];
                if (prefab != null)
                {
                    RegisterPrefab(prefab);
                }
            }
        }

        /// <summary>
        /// Find the registered prefab for this asset id.
        /// Useful for debuggers
        /// </summary>
        /// <param name="assetId">asset id of the prefab</param>
        /// <param name="prefab">the prefab gameobject</param>
        /// <returns>true if prefab was registered</returns>
        public bool GetPrefab(Guid assetId, out GameObject prefab)
        {
            prefab = null;
            return assetId != Guid.Empty &&
                   prefabs.TryGetValue(assetId, out prefab) && prefab != null;
        }

        /// <summary>
        /// Registers a prefab with the spawning system.
        /// <para>When a NetworkIdentity object is spawned on a server with NetworkServer.SpawnObject(), and the prefab that the object was created from was registered with RegisterPrefab(), the client will use that prefab to instantiate a corresponding client object with the same netId.</para>
        /// <para>The NetworkManager has a list of spawnable prefabs, it uses this function to register those prefabs with the ClientScene.</para>
        /// <para>The set of current spawnable object is available in the ClientScene static member variable ClientScene.prefabs, which is a dictionary of NetworkAssetIds and prefab references.</para>
        /// </summary>
        /// <param name="prefab">A Prefab that will be spawned.</param>
        /// <param name="newAssetId">An assetId to be assigned to this prefab. This allows a dynamically created game object to be registered for an already known asset Id.</param>
        public void RegisterPrefab(GameObject prefab, Guid newAssetId)
        {
            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity)
            {
                identity.AssetId = newAssetId;

                if (logger.LogEnabled()) logger.Log("Registering prefab '" + prefab.name + "' as asset:" + identity.AssetId);
                prefabs[identity.AssetId] = prefab;
            }
            else
            {
                throw new InvalidOperationException("Could not register '" + prefab.name + "' since it contains no NetworkIdentity component");
            }
        }

        /// <summary>
        /// Registers a prefab with the spawning system.
        /// <para>When a NetworkIdentity object is spawned on a server with NetworkServer.SpawnObject(), and the prefab that the object was created from was registered with RegisterPrefab(), the client will use that prefab to instantiate a corresponding client object with the same netId.</para>
        /// <para>The NetworkManager has a list of spawnable prefabs, it uses this function to register those prefabs with the ClientScene.</para>
        /// <para>The set of current spawnable object is available in the ClientScene static member variable ClientScene.prefabs, which is a dictionary of NetworkAssetIds and prefab references.</para>
        /// </summary>
        /// <param name="prefab">A Prefab that will be spawned.</param>
        public void RegisterPrefab(GameObject prefab)
        {
            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity)
            {
                if (logger.LogEnabled()) logger.Log("Registering prefab '" + prefab.name + "' as asset:" + identity.AssetId);
                prefabs[identity.AssetId] = prefab;

                NetworkIdentity[] identities = prefab.GetComponentsInChildren<NetworkIdentity>();
                if (identities.Length > 1)
                {
                    logger.LogWarning("The prefab '" + prefab.name +
                                     "' has multiple NetworkIdentity components. There can only be one NetworkIdentity on a prefab, and it must be on the root object.");
                }
            }
            else
            {
                throw new InvalidOperationException("Could not register '" + prefab.name + "' since it contains no NetworkIdentity component");
            }
        }

        /// <summary>
        /// Registers a prefab with the spawning system.
        /// <para>When a NetworkIdentity object is spawned on a server with NetworkServer.SpawnObject(), and the prefab that the object was created from was registered with RegisterPrefab(), the client will use that prefab to instantiate a corresponding client object with the same netId.</para>
        /// <para>The NetworkManager has a list of spawnable prefabs, it uses this function to register those prefabs with the ClientScene.</para>
        /// <para>The set of current spawnable object is available in the ClientScene static member variable ClientScene.prefabs, which is a dictionary of NetworkAssetIds and prefab references.</para>
        /// </summary>
        /// <param name="prefab">A Prefab that will be spawned.</param>
        /// <param name="spawnHandler">A method to use as a custom spawnhandler on clients.</param>
        /// <param name="unspawnHandler">A method to use as a custom un-spawnhandler on clients.</param>
        public void RegisterPrefab(GameObject prefab, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            RegisterPrefab(prefab, msg => spawnHandler(msg.position, msg.assetId), unspawnHandler);
        }

        /// <summary>
        /// Registers a prefab with the spawning system.
        /// <para>When a NetworkIdentity object is spawned on a server with NetworkServer.SpawnObject(), and the prefab that the object was created from was registered with RegisterPrefab(), the client will use that prefab to instantiate a corresponding client object with the same netId.</para>
        /// <para>The NetworkManager has a list of spawnable prefabs, it uses this function to register those prefabs with the ClientScene.</para>
        /// <para>The set of current spawnable object is available in the ClientScene static member variable ClientScene.prefabs, which is a dictionary of NetworkAssetIds and prefab references.</para>
        /// </summary>
        /// <param name="prefab">A Prefab that will be spawned.</param>
        /// <param name="spawnHandler">A method to use as a custom spawnhandler on clients.</param>
        /// <param name="unspawnHandler">A method to use as a custom un-spawnhandler on clients.</param>
        public void RegisterPrefab(GameObject prefab, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                throw new InvalidOperationException("Could not register '" + prefab.name + "' since it contains no NetworkIdentity component");
            }

            if (identity.AssetId == Guid.Empty)
            {
                throw new InvalidOperationException("RegisterPrefab game object " + prefab.name + " has no " + nameof(prefab) + ". Use RegisterSpawnHandler() instead?");
            }

            if (logger.LogEnabled()) logger.Log("Registering custom prefab '" + prefab.name + "' as asset:" + identity.AssetId + " " + spawnHandler.GetMethodName() + "/" + unspawnHandler.GetMethodName());

            spawnHandlers[identity.AssetId] = spawnHandler;
            unspawnHandlers[identity.AssetId] = unspawnHandler;
        }

        /// <summary>
        /// Removes a registered spawn prefab that was setup with ClientScene.RegisterPrefab.
        /// </summary>
        /// <param name="prefab">The prefab to be removed from registration.</param>
        public void UnregisterPrefab(GameObject prefab)
        {
            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                throw new InvalidOperationException("Could not unregister '" + prefab.name + "' since it contains no NetworkIdentity component");
            }
            spawnHandlers.Remove(identity.AssetId);
            unspawnHandlers.Remove(identity.AssetId);
        }

        #endregion

        #region Spawn Handler

        /// <summary>
        /// This is an advanced spawning function that registers a custom assetId with the UNET spawning system.
        /// <para>This can be used to register custom spawning methods for an assetId - instead of the usual method of registering spawning methods for a prefab. This should be used when no prefab exists for the spawned objects - such as when they are constructed dynamically at runtime from configuration data.</para>
        /// </summary>
        /// <param name="assetId">Custom assetId string.</param>
        /// <param name="spawnHandler">A method to use as a custom spawnhandler on clients.</param>
        /// <param name="unspawnHandler">A method to use as a custom un-spawnhandler on clients.</param>
        public void RegisterSpawnHandler(Guid assetId, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            RegisterSpawnHandler(assetId, msg => spawnHandler(msg.position, msg.assetId), unspawnHandler);
        }

        /// <summary>
        /// This is an advanced spawning function that registers a custom assetId with the UNET spawning system.
        /// <para>This can be used to register custom spawning methods for an assetId - instead of the usual method of registering spawning methods for a prefab. This should be used when no prefab exists for the spawned objects - such as when they are constructed dynamically at runtime from configuration data.</para>
        /// </summary>
        /// <param name="assetId">Custom assetId string.</param>
        /// <param name="spawnHandler">A method to use as a custom spawnhandler on clients.</param>
        /// <param name="unspawnHandler">A method to use as a custom un-spawnhandler on clients.</param>
        public void RegisterSpawnHandler(Guid assetId, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            if (logger.LogEnabled()) logger.Log("RegisterSpawnHandler asset '" + assetId + "' " + spawnHandler.GetMethodName() + "/" + unspawnHandler.GetMethodName());

            spawnHandlers[assetId] = spawnHandler;
            unspawnHandlers[assetId] = unspawnHandler;
        }

        /// <summary>
        /// Removes a registered spawn handler function that was registered with ClientScene.RegisterHandler().
        /// </summary>
        /// <param name="assetId">The assetId for the handler to be removed for.</param>
        public void UnregisterSpawnHandler(Guid assetId)
        {
            spawnHandlers.Remove(assetId);
            unspawnHandlers.Remove(assetId);
        }

        /// <summary>
        /// This clears the registered spawn prefabs and spawn handler functions for this client.
        /// </summary>
        public void ClearSpawners()
        {
            prefabs.Clear();
            spawnHandlers.Clear();
            unspawnHandlers.Clear();
        }

        #endregion

        void UnSpawn(NetworkIdentity identity)
        {
            Guid assetId = identity.AssetId;

            identity.StopClient();
            if (unspawnHandlers.TryGetValue(assetId, out UnSpawnDelegate handler) && handler != null)
            {
                handler(identity.gameObject);
            }
            else if (identity.sceneId == 0)
            {
                Destroy(identity.gameObject);
            }
            else
            {
                identity.Reset();
                identity.gameObject.SetActive(false);
                spawnableObjects[identity.sceneId] = identity;
            }
        }

        /// <summary>
        /// Destroys all networked objects on the client.
        /// <para>This can be used to clean up when a network connection is closed.</para>
        /// </summary>
        public void DestroyAllClientObjects()
        {
            foreach (NetworkIdentity identity in Spawned.Values)
            {
                if (identity != null && identity.gameObject != null)
                {
                    UnSpawn(identity);
                }
            }
            Spawned.Clear();
        }

        void ApplySpawnPayload(NetworkIdentity identity, SpawnMessage msg)
        {
            if (msg.assetId != Guid.Empty)
                identity.AssetId = msg.assetId;

            if (!identity.gameObject.activeSelf)
            {
                identity.gameObject.SetActive(true);
            }

            // apply local values for VR support
            identity.transform.localPosition = msg.position;
            identity.transform.localRotation = msg.rotation;
            identity.transform.localScale = msg.scale;
            identity.HasAuthority = msg.isOwner;
            identity.NetId = msg.netId;
            identity.Client = this;

            if (msg.isLocalPlayer)
                InternalAddPlayer(identity);

            // deserialize components if any payload
            // (Count is 0 if there were no components)
            if (msg.payload.Count > 0)
            {
                using (PooledNetworkReader payloadReader = NetworkReaderPool.GetReader(msg.payload))
                {
                    identity.OnDeserializeAllSafely(payloadReader, true);
                }
            }

            Spawned[msg.netId] = identity;

            // objects spawned as part of initial state are started on a second pass
            identity.NotifyAuthority();
            identity.StartClient();
            CheckForLocalPlayer(identity);
        }

        internal void OnSpawn(SpawnMessage msg)
        {
            if (msg.assetId == Guid.Empty && msg.sceneId == 0)
            {
                throw new InvalidOperationException("OnObjSpawn netId: " + msg.netId + " has invalid asset Id");
            }
            if (logger.LogEnabled()) logger.Log($"Client spawn handler instantiating netId={msg.netId} assetID={msg.assetId} sceneId={msg.sceneId} pos={msg.position}");

            // was the object already spawned?
            NetworkIdentity identity = GetExistingObject(msg.netId);

            if (identity == null)
            {
                identity = msg.sceneId == 0 ? SpawnPrefab(msg) : SpawnSceneObject(msg);
            }

            if (identity == null)
            {
                throw new InvalidOperationException($"Could not spawn assetId={msg.assetId} scene={msg.sceneId} netId={msg.netId}");
            }

            ApplySpawnPayload(identity, msg);
        }

        NetworkIdentity GetExistingObject(uint netid)
        {
            Spawned.TryGetValue(netid, out NetworkIdentity localObject);
            return localObject;
        }

        NetworkIdentity SpawnPrefab(SpawnMessage msg)
        {
            if (spawnHandlers.TryGetValue(msg.assetId, out SpawnHandlerDelegate handler))
            {
                GameObject obj = handler(msg);
                if (obj == null)
                {
                    logger.LogWarning("Client spawn handler for " + msg.assetId + " returned null");
                    return null;
                }
                return obj.GetComponent<NetworkIdentity>();
            }
            if (GetPrefab(msg.assetId, out GameObject prefab))
            {
                GameObject obj = Object.Instantiate(prefab, msg.position, msg.rotation);
                if (logger.LogEnabled())
                {
                    logger.Log("Client spawn handler instantiating [netId:" + msg.netId + " asset ID:" + msg.assetId + " pos:" + msg.position + " rotation: " + msg.rotation + "]");
                }

                return obj.GetComponent<NetworkIdentity>();
            }
            logger.LogError("Failed to spawn server object, did you forget to add it to the NetworkManager? assetId=" + msg.assetId + " netId=" + msg.netId);
            return null;
        }

        NetworkIdentity SpawnSceneObject(SpawnMessage msg)
        {
            NetworkIdentity spawnedId = SpawnSceneObject(msg.sceneId);
            if (spawnedId == null)
            {
                logger.LogError("Spawn scene object not found for " + msg.sceneId.ToString("X") + " SpawnableObjects.Count=" + spawnableObjects.Count);

                // dump the whole spawnable objects dict for easier debugging
                if (logger.LogEnabled())
                {
                    foreach (KeyValuePair<ulong, NetworkIdentity> kvp in spawnableObjects)
                        logger.Log("Spawnable: SceneId=" + kvp.Key + " name=" + kvp.Value.name);
                }
            }

            if (logger.LogEnabled()) logger.Log("Client spawn for [netId:" + msg.netId + "] [sceneId:" + msg.sceneId + "] obj:" + spawnedId);
            return spawnedId;
        }

        NetworkIdentity SpawnSceneObject(ulong sceneId)
        {
            if (spawnableObjects.TryGetValue(sceneId, out NetworkIdentity identity))
            {
                spawnableObjects.Remove(sceneId);
                return identity;
            }
            logger.LogWarning("Could not find scene object with sceneid:" + sceneId.ToString("X"));
            return null;
        }

        internal void OnObjectHide(ObjectHideMessage msg)
        {
            DestroyObject(msg.netId);
        }

        internal void OnObjectDestroy(ObjectDestroyMessage msg)
        {
            DestroyObject(msg.netId);
        }

        void DestroyObject(uint netId)
        {
            if (logger.LogEnabled()) logger.Log("ClientScene.OnObjDestroy netId:" + netId);

            if (Spawned.TryGetValue(netId, out NetworkIdentity localObject) && localObject != null)
            {
                UnSpawn(localObject);
                Spawned.Remove(netId);
            }
            else
            {
                logger.LogWarning("Did not find target for destroy message for " + netId);
            }
        }

        internal void OnHostClientObjectDestroy(ObjectDestroyMessage msg)
        {
            if (logger.LogEnabled()) logger.Log("ClientScene.OnLocalObjectObjDestroy netId:" + msg.netId);

            Spawned.Remove(msg.netId);
        }

        internal void OnHostClientObjectHide(ObjectHideMessage msg)
        {
            if (logger.LogEnabled()) logger.Log("ClientScene::OnLocalObjectObjHide netId:" + msg.netId);

            if (Spawned.TryGetValue(msg.netId, out NetworkIdentity localObject) && localObject != null)
            {
                localObject.OnSetHostVisibility(false);
            }
        }

        internal void OnHostClientSpawn(SpawnMessage msg)
        {
            if (Spawned.TryGetValue(msg.netId, out NetworkIdentity localObject) && localObject != null)
            {
                if (msg.isLocalPlayer)
                    InternalAddPlayer(localObject);

                localObject.HasAuthority = msg.isOwner;
                localObject.NotifyAuthority();
                localObject.StartClient();
                localObject.OnSetHostVisibility(true);
                CheckForLocalPlayer(localObject);
            }
        }

        internal void OnUpdateVarsMessage(UpdateVarsMessage msg)
        {
            if (logger.LogEnabled()) logger.Log("ClientScene.OnUpdateVarsMessage " + msg.netId);

            if (Spawned.TryGetValue(msg.netId, out NetworkIdentity localObject) && localObject != null)
            {
                using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(msg.payload))
                    localObject.OnDeserializeAllSafely(networkReader, false);
            }
            else
            {
                logger.LogWarning("Did not find target for sync message for " + msg.netId + " . Note: this can be completely normal because UDP messages may arrive out of order, so this message might have arrived after a Destroy message.");
            }
        }

        internal void OnRpcMessage(RpcMessage msg)
        {
            if (logger.LogEnabled()) logger.Log("ClientScene.OnRPCMessage hash:" + msg.functionHash + " netId:" + msg.netId);

            if (Spawned.TryGetValue(msg.netId, out NetworkIdentity identity) && identity != null)
            {
                using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(msg.payload))
                    identity.HandleRemoteCall(msg.componentIndex, msg.functionHash, MirrorInvokeType.ClientRpc, networkReader);
            }
        }

        void CheckForLocalPlayer(NetworkIdentity identity)
        {
            if (identity && identity == LocalPlayer)
            {
                // Set isLocalPlayer to true on this NetworkIdentity and trigger OnStartLocalPlayer in all scripts on the same GO
                identity.ConnectionToServer = Connection;
                identity.StartLocalPlayer();

                if (logger.LogEnabled()) logger.Log("ClientScene.OnOwnerMessage - player=" + identity.name);
            }
        }
    }
}
