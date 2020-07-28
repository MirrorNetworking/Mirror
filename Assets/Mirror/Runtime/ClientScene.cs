using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using Guid = System.Guid;
using Object = UnityEngine.Object;

namespace Mirror
{
    /// <summary>
    /// A client manager which contains static client information and functions.
    /// <para>This manager contains references to tracked static local objects such as spawner registrations. It also has the default message handlers used by clients when they registered none themselves. The manager handles adding/removing player objects to the game after a client connection has been set as ready.</para>
    /// <para>The ClientScene is a singleton, and it has static convenience methods such as ClientScene.Ready().</para>
    /// <para>The ClientScene is used by the NetworkManager, but it can be used by itself.</para>
    /// <para>As the ClientScene manages player objects on the client, it is where clients request to add players. The NetworkManager does this via the ClientScene automatically when auto-add-players is set, but it can be done through code using the function ClientScene.AddPlayer(). This sends an AddPlayer message to the server and will cause a player object to be created for this client.</para>
    /// <para>Like NetworkServer, the ClientScene understands the concept of the local client. The function ClientScene.ConnectLocalServer() is used to become a host by starting a local client (when a server is already running).</para>
    /// </summary>
    public static class ClientScene
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(ClientScene));

        static bool isSpawnFinished;
        static NetworkIdentity _localPlayer;

        /// <summary>
        /// NetworkIdentity of the localPlayer
        /// </summary>
        public static NetworkIdentity localPlayer
        {
            get => _localPlayer;
            private set
            {
                NetworkIdentity oldPlayer = _localPlayer;
                NetworkIdentity newPlayer = value;
                if (oldPlayer != newPlayer)
                {
                    _localPlayer = value;
                    onLocalPlayerChanged?.Invoke(oldPlayer, newPlayer);
                }
            }
        }

        public delegate void LocalplayerChanged(NetworkIdentity oldPlayer, NetworkIdentity newPlayer);
        public static event LocalplayerChanged onLocalPlayerChanged;

        /// <summary>
        /// Returns true when a client's connection has been set to ready.
        /// <para>A client that is ready recieves state updates from the server, while a client that is not ready does not. This useful when the state of the game is not normal, such as a scene change or end-of-game.</para>
        /// <para>This is read-only. To change the ready state of a client, use ClientScene.Ready(). The server is able to set the ready state of clients using NetworkServer.SetClientReady(), NetworkServer.SetClientNotReady() and NetworkServer.SetAllClientsNotReady().</para>
        /// <para>This is done when changing scenes so that clients don't receive state update messages during scene loading.</para>
        /// </summary>
        public static bool ready { get; set; }

        /// <summary>
        /// The NetworkConnection object that is currently "ready". This is the connection to the server where objects are spawned from.
        /// <para>This connection can be used to send messages to the server. There can only be one ClientScene and ready connection at a time.</para>
        /// </summary>
        public static NetworkConnection readyConnection { get; private set; }

        /// <summary>
        /// This is a dictionary of the prefabs that are registered on the client with ClientScene.RegisterPrefab().
        /// <para>The key to the dictionary is the prefab asset Id.</para>
        /// </summary>
        public static readonly Dictionary<Guid, GameObject> prefabs = new Dictionary<Guid, GameObject>();

        /// <summary>
        /// This is dictionary of the disabled NetworkIdentity objects in the scene that could be spawned by messages from the server.
        /// <para>The key to the dictionary is the NetworkIdentity sceneId.</para>
        /// </summary>
        public static readonly Dictionary<ulong, NetworkIdentity> spawnableObjects = new Dictionary<ulong, NetworkIdentity>();

        // spawn handlers
        internal static readonly Dictionary<Guid, SpawnHandlerDelegate> spawnHandlers = new Dictionary<Guid, SpawnHandlerDelegate>();
        internal static readonly Dictionary<Guid, UnSpawnDelegate> unspawnHandlers = new Dictionary<Guid, UnSpawnDelegate>();

        internal static void Shutdown()
        {
            ClearSpawners();
            spawnableObjects.Clear();
            readyConnection = null;
            ready = false;
            isSpawnFinished = false;
            DestroyAllClientObjects();
        }

        // this is called from message handler for Owner message
        internal static void InternalAddPlayer(NetworkIdentity identity)
        {
            logger.Log("ClientScene.InternalAddPlayer");

            // NOTE: It can be "normal" when changing scenes for the player to be destroyed and recreated.
            // But, the player structures are not cleaned up, we'll just replace the old player
            localPlayer = identity;

            // NOTE: we DONT need to set isClient=true here, because OnStartClient
            // is called before OnStartLocalPlayer, hence it's already set.
            // localPlayer.isClient = true;

            if (readyConnection != null)
            {
                readyConnection.identity = identity;
            }
            else
            {
                logger.LogWarning("No ready connection found for setting player controller during InternalAddPlayer");
            }
        }

        /// <summary>
        /// This adds a player GameObject for this client. This causes an AddPlayer message to be sent to the server, and NetworkManager.OnServerAddPlayer is called. If an extra message was passed to AddPlayer, then OnServerAddPlayer will be called with a NetworkReader that contains the contents of the message.
        /// <para>extraMessage can contain character selection, etc.</para>
        /// </summary>
        /// <param name="readyConn">The connection to become ready for this client.</param>
        /// <returns>True if player was added.</returns>
        public static bool AddPlayer(NetworkConnection readyConn)
        {
            // ensure valid ready connection
            if (readyConn != null)
            {
                ready = true;
                readyConnection = readyConn;
            }

            if (!ready)
            {
                logger.LogError("Must call AddPlayer() with a connection the first time to become ready.");
                return false;
            }

            if (readyConnection.identity != null)
            {
                logger.LogError("ClientScene.AddPlayer: a PlayerController was already added. Did you call AddPlayer twice?");
                return false;
            }

            if (logger.LogEnabled()) logger.Log("ClientScene.AddPlayer() called with connection [" + readyConnection + "]");

            readyConnection.Send(new AddPlayerMessage());
            return true;
        }

        // Deprecated 5/2/2020
        /// <summary>
        /// Obsolete: Removed as a security risk. Use <see cref="NetworkServer.RemovePlayerForConnection(NetworkConnection, bool)">NetworkServer.RemovePlayerForConnection</see> instead.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("Removed as a security risk. Use NetworkServer.RemovePlayerForConnection(NetworkConnection conn, bool keepAuthority = false) instead", true)]
        public static bool RemovePlayer() { return false; }

        /// <summary>
        /// Signal that the client connection is ready to enter the game.
        /// <para>This could be for example when a client enters an ongoing game and has finished loading the current scene. The server should respond to the SYSTEM_READY event with an appropriate handler which instantiates the players object for example.</para>
        /// </summary>
        /// <param name="conn">The client connection which is ready.</param>
        /// <returns>True if succcessful</returns>
        public static bool Ready(NetworkConnection conn)
        {
            if (ready)
            {
                logger.LogError("A connection has already been set as ready. There can only be one.");
                return false;
            }

            if (logger.LogEnabled()) logger.Log("ClientScene.Ready() called with connection [" + conn + "]");

            if (conn != null)
            {
                // Set these before sending the ReadyMessage, otherwise host client
                // will fail in InternalAddPlayer with null readyConnection.
                ready = true;
                readyConnection = conn;
                readyConnection.isReady = true;

                // Tell server we're ready to have a player object spawned
                conn.Send(new ReadyMessage());

                return true;
            }
            logger.LogError("Ready() called with invalid connection object: conn=null");
            return false;
        }

        internal static void HandleClientDisconnect(NetworkConnection conn)
        {
            if (readyConnection == conn && ready)
            {
                ready = false;
                readyConnection = null;
            }
        }

        /// <summary>
        /// Checks if identity is not spawned yet, not hidden and has sceneId
        /// </summary>
        static bool ConsiderForSpawning(NetworkIdentity identity)
        {
            // not spawned yet, not hidden, etc.?
            return !identity.gameObject.activeSelf &&
                   identity.gameObject.hideFlags != HideFlags.NotEditable &&
                   identity.gameObject.hideFlags != HideFlags.HideAndDontSave &&
                   identity.sceneId != 0;
        }

        /// <summary>
        /// Call this after loading/unloading a scene in the client after connection to register the spawnable objects
        /// </summary>
        public static void PrepareToSpawnSceneObjects()
        {
            // remove existing items, they will be re-added below
            spawnableObjects.Clear();

            // finds all NetworkIdentity currently loaded by unity (includes disabled objects)
            NetworkIdentity[] allIdentities = Resources.FindObjectsOfTypeAll<NetworkIdentity>();
            foreach (NetworkIdentity identity in allIdentities)
            {
                // add all unspawned NetworkIdentities to spawnable objects
                if (ConsiderForSpawning(identity))
                {
                    spawnableObjects.Add(identity.sceneId, identity);
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
        public static bool GetPrefab(Guid assetId, out GameObject prefab)
        {
            prefab = null;
            return assetId != Guid.Empty &&
                   prefabs.TryGetValue(assetId, out prefab) && prefab != null;
        }

        /// <summary>
        /// Valids Prefab then adds it to prefabs dictionary 
        /// </summary>
        /// <param name="prefab">NetworkIdentity on Prefab GameObject</param>
        static void RegisterPrefabIdentity(NetworkIdentity prefab)
        {
            if (prefab.assetId == Guid.Empty)
            {
                logger.LogError($"Can not Register '{prefab.name}' because it had empty assetid. If this is a scene Object use RegisterSpawnHandler instead");
                return;
            }

            if (prefab.sceneId != 0)
            {
                logger.LogError($"Can not Register '{prefab.name}' because it has a sceneId, make sure you are passing in the original prefab and not an instance in the scene.");
                return;
            }

            NetworkIdentity[] identities = prefab.GetComponentsInChildren<NetworkIdentity>();
            if (identities.Length > 1)
            {
                logger.LogWarning($"Prefab '{prefab.name}' has multiple NetworkIdentity components. There should only be one NetworkIdentity on a prefab, and it must be on the root object.");
            }

            if (prefabs.ContainsKey(prefab.assetId))
            {
                GameObject existingPrefab = prefabs[prefab.assetId];
                logger.LogWarning($"Replacing existing prefab with assetId '{prefab.assetId}'. Old prefab '{existingPrefab.name}', New prefab '{prefab.name}'");
            }

            if (spawnHandlers.ContainsKey(prefab.assetId) || unspawnHandlers.ContainsKey(prefab.assetId))
            {
                logger.LogWarning($"Adding prefab '{prefab.name}' with assetId '{prefab.assetId}' when spawnHandlers with same assetId already exists.");
            }

            if (logger.LogEnabled()) logger.Log($"Registering prefab '{prefab.name}' as asset:{prefab.assetId}");

            prefabs[prefab.assetId] = prefab.gameObject;
        }

        /// <summary>
        /// Registers a prefab with the spawning system.
        /// <para>When a NetworkIdentity object is spawned on a server with NetworkServer.SpawnObject(), and the prefab that the object was created from was registered with RegisterPrefab(), the client will use that prefab to instantiate a corresponding client object with the same netId.</para>
        /// <para>The NetworkManager has a list of spawnable prefabs, it uses this function to register those prefabs with the ClientScene.</para>
        /// <para>The set of current spawnable object is available in the ClientScene static member variable ClientScene.prefabs, which is a dictionary of NetworkAssetIds and prefab references.</para>
        /// <para>NOTE: newAssetId can not be set on GameObjects that already have an assetId</para>
        /// </summary>
        /// <param name="prefab">A GameObject that will be spawned.</param>
        /// <param name="newAssetId">An assetId to be assigned to this GameObject. This allows a dynamically created game object to be registered for an already known asset Id.</param>
        public static void RegisterPrefab(GameObject prefab, Guid newAssetId)
        {
            if (prefab == null)
            {
                logger.LogError("Could not register prefab because it was null");
                return;
            }

            if (newAssetId == Guid.Empty)
            {
                logger.LogError($"Could not register '{prefab.name}' with new assetId because the new assetId was empty");
                return;
            }

            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                logger.LogError($"Could not register '{prefab.name}' since it contains no NetworkIdentity component");
                return;
            }

            if (identity.assetId != Guid.Empty && identity.assetId != newAssetId)
            {
                logger.LogError($"Could not register '{prefab.name}' to {newAssetId} because it already had an AssetId, Existing assetId {identity.assetId}");
                return;
            }

            identity.assetId = newAssetId;

            RegisterPrefabIdentity(identity);
        }

        /// <summary>
        /// Registers a prefab with the spawning system.
        /// <para>When a NetworkIdentity object is spawned on a server with NetworkServer.SpawnObject(), and the prefab that the object was created from was registered with RegisterPrefab(), the client will use that prefab to instantiate a corresponding client object with the same netId.</para>
        /// <para>The NetworkManager has a list of spawnable prefabs, it uses this function to register those prefabs with the ClientScene.</para>
        /// <para>The set of current spawnable object is available in the ClientScene static member variable ClientScene.prefabs, which is a dictionary of NetworkAssetIds and prefab references.</para>
        /// </summary>
        /// <param name="prefab">A Prefab that will be spawned.</param>
        public static void RegisterPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                logger.LogError("Could not register prefab because it was null");
                return;
            }

            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                logger.LogError($"Could not register '{prefab.name}' since it contains no NetworkIdentity component");
                return;
            }

            RegisterPrefabIdentity(identity);
        }

        /// <summary>
        /// Registers a prefab with the spawning system.
        /// <para>When a NetworkIdentity object is spawned on a server with NetworkServer.SpawnObject(), and the prefab that the object was created from was registered with RegisterPrefab(), the client will use that prefab to instantiate a corresponding client object with the same netId.</para>
        /// <para>The NetworkManager has a list of spawnable prefabs, it uses this function to register those prefabs with the ClientScene.</para>
        /// <para>The set of current spawnable object is available in the ClientScene static member variable ClientScene.prefabs, which is a dictionary of NetworkAssetIds and prefab references.</para>
        /// <para>NOTE: newAssetId can not be set on GameObjects that already have an assetId</para>
        /// </summary>
        /// <param name="prefab">A GameObject that will be spawned.</param>
        /// <param name="newAssetId">An assetId to be assigned to this GameObject. This allows a dynamically created game object to be registered for an already known asset Id.</param>
        /// <param name="spawnHandler">A method to use as a custom spawnhandler on clients.</param>
        /// <param name="unspawnHandler">A method to use as a custom un-spawnhandler on clients.</param>
        public static void RegisterPrefab(GameObject prefab, Guid newAssetId, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            // We need this check here because we don't want a null handler in the lambda expression below
            if (spawnHandler == null)
            {
                logger.LogError($"Can not Register null SpawnHandler for {newAssetId}");
                return;
            }

            RegisterPrefab(prefab, newAssetId, msg => spawnHandler(msg.position, msg.assetId), unspawnHandler);
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
        public static void RegisterPrefab(GameObject prefab, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            if (prefab == null)
            {
                logger.LogError("Could not register handler for prefab because the prefab was null");
                return;
            }

            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                logger.LogError("Could not register handler for '" + prefab.name + "' since it contains no NetworkIdentity component");
                return;
            }

            if (identity.sceneId != 0)
            {
                logger.LogError($"Can not Register '{prefab.name}' because it has a sceneId, make sure you are passing in the original prefab and not an instance in the scene.");
                return;
            }

            Guid assetId = identity.assetId;

            if (assetId == Guid.Empty)
            {
                logger.LogError($"Can not Register handler for '{prefab.name}' because it had empty assetid. If this is a scene Object use RegisterSpawnHandler instead");
                return;
            }

            // We need this check here because we don't want a null handler in the lambda expression below
            if (spawnHandler == null)
            {
                logger.LogError($"Can not Register null SpawnHandler for {assetId}");
                return;
            }

            RegisterPrefab(prefab, msg => spawnHandler(msg.position, msg.assetId), unspawnHandler);
        }

        /// <summary>
        /// Registers a prefab with the spawning system.
        /// <para>When a NetworkIdentity object is spawned on a server with NetworkServer.SpawnObject(), and the prefab that the object was created from was registered with RegisterPrefab(), the client will use that prefab to instantiate a corresponding client object with the same netId.</para>
        /// <para>The NetworkManager has a list of spawnable prefabs, it uses this function to register those prefabs with the ClientScene.</para>
        /// <para>The set of current spawnable object is available in the ClientScene static member variable ClientScene.prefabs, which is a dictionary of NetworkAssetIds and prefab references.</para>
        /// <para>NOTE: newAssetId can not be set on GameObjects that already have an assetId</para>
        /// </summary>
        /// <param name="prefab">A GameObject that will be spawned.</param>
        /// <param name="newAssetId">An assetId to be assigned to this GameObject. This allows a dynamically created game object to be registered for an already known asset Id.</param>
        /// <param name="spawnHandler">A method to use as a custom spawnhandler on clients.</param>
        /// <param name="unspawnHandler">A method to use as a custom un-spawnhandler on clients.</param>
        public static void RegisterPrefab(GameObject prefab, Guid newAssetId, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            if (newAssetId == Guid.Empty)
            {
                logger.LogError($"Could not register handler for '{prefab.name}' with new assetId because the new assetId was empty");
                return;
            }

            if (prefab == null)
            {
                logger.LogError("Could not register handler for prefab because the prefab was null");
                return;
            }

            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                logger.LogError("Could not register handler for '" + prefab.name + "' since it contains no NetworkIdentity component");
                return;
            }

            if (identity.assetId != Guid.Empty && identity.assetId != newAssetId)
            {
                logger.LogError($"Could not register Handler for '{prefab.name}' to {newAssetId} because it already had an AssetId, Existing assetId {identity.assetId}");
                return;
            }

            if (identity.sceneId != 0)
            {
                logger.LogError($"Can not Register '{prefab.name}' because it has a sceneId, make sure you are passing in the original prefab and not an instance in the scene.");
                return;
            }

            identity.assetId = newAssetId;
            Guid assetId = identity.assetId;

            if (spawnHandler == null)
            {
                logger.LogError($"Can not Register null SpawnHandler for {assetId}");
                return;
            }

            if (unspawnHandler == null)
            {
                logger.LogError($"Can not Register null UnSpawnHandler for {assetId}");
                return;
            }

            if (spawnHandlers.ContainsKey(assetId) || unspawnHandlers.ContainsKey(assetId))
            {
                logger.LogWarning($"Replacing existing spawnHandlers for prefab '{prefab.name}' with assetId '{assetId}'");
            }

            if (prefabs.ContainsKey(assetId))
            {
                // this is error because SpawnPrefab checks prefabs before handler
                logger.LogError($"assetId '{assetId}' is already used by prefab '{prefabs[assetId].name}', unregister the prefab first before trying to add handler");
            }

            NetworkIdentity[] identities = prefab.GetComponentsInChildren<NetworkIdentity>();
            if (identities.Length > 1)
            {
                logger.LogWarning($"Prefab '{prefab.name}' has multiple NetworkIdentity components. There should only be one NetworkIdentity on a prefab, and it must be on the root object.");
            }

            if (logger.LogEnabled()) logger.Log("Registering custom prefab '" + prefab.name + "' as asset:" + assetId + " " + spawnHandler.GetMethodName() + "/" + unspawnHandler.GetMethodName());

            spawnHandlers[assetId] = spawnHandler;
            unspawnHandlers[assetId] = unspawnHandler;
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
        public static void RegisterPrefab(GameObject prefab, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            if (prefab == null)
            {
                logger.LogError("Could not register handler for prefab because the prefab was null");
                return;
            }

            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                logger.LogError("Could not register handler for '" + prefab.name + "' since it contains no NetworkIdentity component");
                return;
            }

            if (identity.sceneId != 0)
            {
                logger.LogError($"Can not Register '{prefab.name}' because it has a sceneId, make sure you are passing in the original prefab and not an instance in the scene.");
                return;
            }

            Guid assetId = identity.assetId;

            if (assetId == Guid.Empty)
            {
                logger.LogError($"Can not Register handler for '{prefab.name}' because it had empty assetid. If this is a scene Object use RegisterSpawnHandler instead");
                return;
            }

            if (spawnHandler == null)
            {
                logger.LogError($"Can not Register null SpawnHandler for {assetId}");
                return;
            }

            if (unspawnHandler == null)
            {
                logger.LogError($"Can not Register null UnSpawnHandler for {assetId}");
                return;
            }

            if (spawnHandlers.ContainsKey(assetId) || unspawnHandlers.ContainsKey(assetId))
            {
                logger.LogWarning($"Replacing existing spawnHandlers for prefab '{prefab.name}' with assetId '{assetId}'");
            }

            if (prefabs.ContainsKey(assetId))
            {
                // this is error because SpawnPrefab checks prefabs before handler
                logger.LogError($"assetId '{assetId}' is already used by prefab '{prefabs[assetId].name}', unregister the prefab first before trying to add handler");
            }

            NetworkIdentity[] identities = prefab.GetComponentsInChildren<NetworkIdentity>();
            if (identities.Length > 1)
            {
                logger.LogWarning($"Prefab '{prefab.name}' has multiple NetworkIdentity components. There should only be one NetworkIdentity on a prefab, and it must be on the root object.");
            }

            if (logger.LogEnabled()) logger.Log("Registering custom prefab '" + prefab.name + "' as asset:" + assetId + " " + spawnHandler.GetMethodName() + "/" + unspawnHandler.GetMethodName());

            spawnHandlers[assetId] = spawnHandler;
            unspawnHandlers[assetId] = unspawnHandler;
        }

        /// <summary>
        /// Removes a registered spawn prefab that was setup with ClientScene.RegisterPrefab.
        /// </summary>
        /// <param name="prefab">The prefab to be removed from registration.</param>
        public static void UnregisterPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                logger.LogError("Could not unregister prefab because it was null");
                return;
            }

            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                logger.LogError("Could not unregister '" + prefab.name + "' since it contains no NetworkIdentity component");
                return;
            }

            Guid assetId = identity.assetId;

            prefabs.Remove(assetId);
            spawnHandlers.Remove(assetId);
            unspawnHandlers.Remove(assetId);
        }

        /// <summary>
        /// This is an advanced spawning function that registers a custom assetId with the UNET spawning system.
        /// <para>This can be used to register custom spawning methods for an assetId - instead of the usual method of registering spawning methods for a prefab. This should be used when no prefab exists for the spawned objects - such as when they are constructed dynamically at runtime from configuration data.</para>
        /// </summary>
        /// <param name="assetId">Custom assetId string.</param>
        /// <param name="spawnHandler">A method to use as a custom spawnhandler on clients.</param>
        /// <param name="unspawnHandler">A method to use as a custom un-spawnhandler on clients.</param>
        public static void RegisterSpawnHandler(Guid assetId, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            // We need this check here because we don't want a null handler in the lambda expression below
            if (spawnHandler == null)
            {
                logger.LogError($"Can not Register null SpawnHandler for {assetId}");
                return;
            }

            RegisterSpawnHandler(assetId, msg => spawnHandler(msg.position, msg.assetId), unspawnHandler);
        }

        /// <summary>
        /// This is an advanced spawning function that registers a custom assetId with the UNET spawning system.
        /// <para>This can be used to register custom spawning methods for an assetId - instead of the usual method of registering spawning methods for a prefab. This should be used when no prefab exists for the spawned objects - such as when they are constructed dynamically at runtime from configuration data.</para>
        /// </summary>
        /// <param name="assetId">Custom assetId string.</param>
        /// <param name="spawnHandler">A method to use as a custom spawnhandler on clients.</param>
        /// <param name="unspawnHandler">A method to use as a custom un-spawnhandler on clients.</param>
        public static void RegisterSpawnHandler(Guid assetId, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            if (spawnHandler == null)
            {
                logger.LogError($"Can not Register null SpawnHandler for {assetId}");
                return;
            }

            if (unspawnHandler == null)
            {
                logger.LogError($"Can not Register null UnSpawnHandler for {assetId}");
                return;
            }

            if (assetId == Guid.Empty)
            {
                logger.LogError("Can not Register SpawnHandler for empty Guid");
                return;
            }

            if (spawnHandlers.ContainsKey(assetId) || unspawnHandlers.ContainsKey(assetId))
            {
                logger.LogWarning($"Replacing existing spawnHandlers for {assetId}");
            }

            if (prefabs.ContainsKey(assetId))
            {
                // this is error because SpawnPrefab checks prefabs before handler
                logger.LogError($"assetId '{assetId}' is already used by prefab '{prefabs[assetId].name}'");
            }

            if (logger.LogEnabled()) logger.Log("RegisterSpawnHandler asset '" + assetId + "' " + spawnHandler.GetMethodName() + "/" + unspawnHandler.GetMethodName());

            spawnHandlers[assetId] = spawnHandler;
            unspawnHandlers[assetId] = unspawnHandler;
        }

        /// <summary>
        /// Removes a registered spawn handler function that was registered with ClientScene.RegisterHandler().
        /// </summary>
        /// <param name="assetId">The assetId for the handler to be removed for.</param>
        public static void UnregisterSpawnHandler(Guid assetId)
        {
            spawnHandlers.Remove(assetId);
            unspawnHandlers.Remove(assetId);
        }

        /// <summary>
        /// This clears the registered spawn prefabs and spawn handler functions for this client.
        /// </summary>
        public static void ClearSpawners()
        {
            prefabs.Clear();
            spawnHandlers.Clear();
            unspawnHandlers.Clear();
        }

        static bool InvokeUnSpawnHandler(Guid assetId, GameObject obj)
        {
            if (unspawnHandlers.TryGetValue(assetId, out UnSpawnDelegate handler) && handler != null)
            {
                handler(obj);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Destroys all networked objects on the client.
        /// <para>This can be used to clean up when a network connection is closed.</para>
        /// </summary>
        public static void DestroyAllClientObjects()
        {
            // user can modify spawned lists which causes InvalidOperationException
            // list can modified either in UnSpawnHandler or in OnDisable/OnDestroy
            // we need the Try/Catch so that the rest of the shutdown does not get stopped
            try
            {
                foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values)
                {
                    if (identity != null && identity.gameObject != null)
                    {
                        bool wasUnspawned = InvokeUnSpawnHandler(identity.assetId, identity.gameObject);
                        if (!wasUnspawned)
                        {
                            if (identity.sceneId == 0)
                            {
                                Object.Destroy(identity.gameObject);
                            }
                            else
                            {
                                identity.Reset();
                                identity.gameObject.SetActive(false);
                            }
                        }
                    }
                }
                NetworkIdentity.spawned.Clear();
            }
            catch (InvalidOperationException e)
            {
                logger.LogException(e);
                logger.LogError("Could not DestroyAllClientObjects because spawned list was modified during loop, make sure you are not modifying NetworkIdentity.spawned by calling NetworkServer.Destroy or NetworkServer.Spawn in OnDestroy or OnDisable.");
            }
        }

        internal static void ApplySpawnPayload(NetworkIdentity identity, SpawnMessage msg)
        {
            if (msg.assetId != Guid.Empty)
                identity.assetId = msg.assetId;

            if (!identity.gameObject.activeSelf)
            {
                identity.gameObject.SetActive(true);
            }

            // apply local values for VR support
            identity.transform.localPosition = msg.position;
            identity.transform.localRotation = msg.rotation;
            identity.transform.localScale = msg.scale;
            identity.hasAuthority = msg.isOwner;
            identity.netId = msg.netId;

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

            NetworkIdentity.spawned[msg.netId] = identity;

            // objects spawned as part of initial state are started on a second pass
            if (isSpawnFinished)
            {
                identity.NotifyAuthority();
                identity.OnStartClient();
                CheckForLocalPlayer(identity);
            }
        }

        internal static void OnSpawn(SpawnMessage msg)
        {
            if (msg.assetId == Guid.Empty && msg.sceneId == 0)
            {
                logger.LogError("OnObjSpawn netId: " + msg.netId + " has invalid asset Id");
                return;
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
                logger.LogError($"Could not spawn assetId={msg.assetId} scene={msg.sceneId} netId={msg.netId}");
                return;
            }

            ApplySpawnPayload(identity, msg);
        }

        static NetworkIdentity GetExistingObject(uint netid)
        {
            NetworkIdentity.spawned.TryGetValue(netid, out NetworkIdentity localObject);
            return localObject;
        }

        static NetworkIdentity SpawnPrefab(SpawnMessage msg)
        {
            if (GetPrefab(msg.assetId, out GameObject prefab))
            {
                GameObject obj = Object.Instantiate(prefab, msg.position, msg.rotation);
                if (logger.LogEnabled())
                {
                    logger.Log("Client spawn handler instantiating [netId:" + msg.netId + " asset ID:" + msg.assetId + " pos:" + msg.position + " rotation: " + msg.rotation + "]");
                }

                return obj.GetComponent<NetworkIdentity>();
            }
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
            logger.LogError("Failed to spawn server object, did you forget to add it to the NetworkManager? assetId=" + msg.assetId + " netId=" + msg.netId);
            return null;
        }

        static NetworkIdentity SpawnSceneObject(SpawnMessage msg)
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

        static NetworkIdentity SpawnSceneObject(ulong sceneId)
        {
            if (spawnableObjects.TryGetValue(sceneId, out NetworkIdentity identity))
            {
                spawnableObjects.Remove(sceneId);
                return identity;
            }
            logger.LogWarning("Could not find scene object with sceneid:" + sceneId.ToString("X"));
            return null;
        }

        internal static void OnObjectSpawnStarted(ObjectSpawnStartedMessage _)
        {
            if (logger.LogEnabled()) logger.Log("SpawnStarted");

            PrepareToSpawnSceneObjects();
            isSpawnFinished = false;
        }

        internal static void OnObjectSpawnFinished(ObjectSpawnFinishedMessage _)
        {
            logger.Log("SpawnFinished");

            // paul: Initialize the objects in the same order as they were initialized
            // in the server.   This is important if spawned objects
            // use data from scene objects
            foreach (NetworkIdentity identity in NetworkIdentity.spawned.Values.OrderBy(uv => uv.netId))
            {
                identity.NotifyAuthority();
                identity.OnStartClient();
                CheckForLocalPlayer(identity);
            }
            isSpawnFinished = true;
        }

        internal static void OnObjectHide(ObjectHideMessage msg)
        {
            DestroyObject(msg.netId);
        }

        internal static void OnObjectDestroy(ObjectDestroyMessage msg)
        {
            DestroyObject(msg.netId);
        }

        static void DestroyObject(uint netId)
        {
            if (logger.LogEnabled()) logger.Log("ClientScene.OnObjDestroy netId:" + netId);

            if (NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity localObject) && localObject != null)
            {
                localObject.OnStopClient();

                if (!InvokeUnSpawnHandler(localObject.assetId, localObject.gameObject))
                {
                    // default handling
                    if (localObject.sceneId == 0)
                    {
                        Object.Destroy(localObject.gameObject);
                    }
                    else
                    {
                        // scene object.. disable it in scene instead of destroying
                        localObject.gameObject.SetActive(false);
                        spawnableObjects[localObject.sceneId] = localObject;
                    }
                }
                NetworkIdentity.spawned.Remove(netId);
                localObject.Reset();
            }
            else
            {
                if (logger.LogEnabled()) logger.LogWarning("Did not find target for destroy message for " + netId);
            }
        }

        internal static void OnHostClientObjectDestroy(ObjectDestroyMessage msg)
        {
            if (logger.LogEnabled()) logger.Log("ClientScene.OnLocalObjectObjDestroy netId:" + msg.netId);

            NetworkIdentity.spawned.Remove(msg.netId);
        }

        internal static void OnHostClientObjectHide(ObjectHideMessage msg)
        {
            if (logger.LogEnabled()) logger.Log("ClientScene::OnLocalObjectObjHide netId:" + msg.netId);

            if (NetworkIdentity.spawned.TryGetValue(msg.netId, out NetworkIdentity localObject) && localObject != null)
            {
                localObject.OnSetHostVisibility(false);
            }
        }

        internal static void OnHostClientSpawn(SpawnMessage msg)
        {
            if (NetworkIdentity.spawned.TryGetValue(msg.netId, out NetworkIdentity localObject) && localObject != null)
            {
                if (msg.isLocalPlayer)
                    InternalAddPlayer(localObject);

                localObject.hasAuthority = msg.isOwner;
                localObject.NotifyAuthority();
                localObject.OnStartClient();
                localObject.OnSetHostVisibility(true);
                CheckForLocalPlayer(localObject);
            }
        }

        internal static void OnUpdateVarsMessage(UpdateVarsMessage msg)
        {
            if (logger.LogEnabled()) logger.Log("ClientScene.OnUpdateVarsMessage " + msg.netId);

            if (NetworkIdentity.spawned.TryGetValue(msg.netId, out NetworkIdentity localObject) && localObject != null)
            {
                using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(msg.payload))
                    localObject.OnDeserializeAllSafely(networkReader, false);
            }
            else
            {
                logger.LogWarning("Did not find target for sync message for " + msg.netId + " . Note: this can be completely normal because UDP messages may arrive out of order, so this message might have arrived after a Destroy message.");
            }
        }

        internal static void OnRPCMessage(RpcMessage msg)
        {
            if (logger.LogEnabled()) logger.Log("ClientScene.OnRPCMessage hash:" + msg.functionHash + " netId:" + msg.netId);

            if (NetworkIdentity.spawned.TryGetValue(msg.netId, out NetworkIdentity identity))
            {
                using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(msg.payload))
                    identity.HandleRPC(msg.componentIndex, msg.functionHash, networkReader);
            }
        }

        internal static void OnSyncEventMessage(SyncEventMessage msg)
        {
            if (logger.LogEnabled()) logger.Log("ClientScene.OnSyncEventMessage " + msg.netId);

            if (NetworkIdentity.spawned.TryGetValue(msg.netId, out NetworkIdentity identity))
            {
                using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(msg.payload))
                    identity.HandleSyncEvent(msg.componentIndex, msg.functionHash, networkReader);
            }
            else
            {
                logger.LogWarning("Did not find target for SyncEvent message for " + msg.netId);
            }
        }

        static void CheckForLocalPlayer(NetworkIdentity identity)
        {
            if (identity == localPlayer)
            {
                // Set isLocalPlayer to true on this NetworkIdentity and trigger OnStartLocalPlayer in all scripts on the same GO
                identity.connectionToServer = readyConnection;
                identity.OnStartLocalPlayer();

                if (logger.LogEnabled()) logger.Log("ClientScene.OnOwnerMessage - player=" + identity.name);
            }
        }
    }
}
