using System;
using System.Collections.Generic;
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
        static NetworkIdentity _localPlayer;

        /// <summary>
        /// NetworkIdentity of the localPlayer
        /// </summary>
        public static NetworkIdentity localPlayer { get; private set; }

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

        internal static void Shutdown()
        {
            ClearSpawners();
            spawnableObjects.Clear();
            readyConnection = null;
            ready = false;
            DestroyAllClientObjects();
        }

        /// <summary>
        /// this is called from message handler for Owner message
        /// </summary>
        /// <param name="identity"></param>
        internal static void InternalAddPlayer(NetworkIdentity identity)
        {
            Debug.Log("ClientScene.InternalAddPlayer");

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
                Debug.LogWarning("No ready connection found for setting player controller during InternalAddPlayer");
            }
        }

        /// <summary>
        /// Sets localPlayer to null
        /// <para>Should be called when the local player object is destroyed</para>
        /// </summary>
        internal static void ClearLocalPlayer()
        {
            Debug.Log("ClientScene.ClearLocalPlayer");

            localPlayer = null;
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
                Debug.LogError("Must call AddPlayer() with a connection the first time to become ready.");
                return false;
            }

            if (readyConnection.identity != null)
            {
                Debug.LogError("ClientScene.AddPlayer: a PlayerController was already added. Did you call AddPlayer twice?");
                return false;
            }

            // Debug.Log("ClientScene.AddPlayer() called with connection [" + readyConnection + "]");

            readyConnection.Send(new AddPlayerMessage());
            return true;
        }

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
                Debug.LogError("A connection has already been set as ready. There can only be one.");
                return false;
            }

            // Debug.Log("ClientScene.Ready() called with connection [" + conn + "]");

            if (conn != null)
            {
                // find spawnable scene objects before joining the world.
                // this way we are able to properly react to SpawnMessages.
                PrepareToSpawnSceneObjects();

                // Set these before sending the ReadyMessage, otherwise host client
                // will fail in InternalAddPlayer with null readyConnection.
                ready = true;
                readyConnection = conn;
                readyConnection.isReady = true;

                // Tell server we're ready to have a player object spawned
                conn.Send(new ReadyMessage());

                return true;
            }
            Debug.LogError("Ready() called with invalid connection object: conn=null");
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
                Debug.LogError($"Can not Register '{prefab.name}' because it had empty assetid. If this is a scene Object use RegisterSpawnHandler instead");
                return;
            }

            if (prefab.sceneId != 0)
            {
                Debug.LogError($"Can not Register '{prefab.name}' because it has a sceneId, make sure you are passing in the original prefab and not an instance in the scene.");
                return;
            }

            NetworkIdentity[] identities = prefab.GetComponentsInChildren<NetworkIdentity>();
            if (identities.Length > 1)
            {
                Debug.LogWarning($"Prefab '{prefab.name}' has multiple NetworkIdentity components. There should only be one NetworkIdentity on a prefab, and it must be on the root object.");
            }

            if (prefabs.ContainsKey(prefab.assetId))
            {
                GameObject existingPrefab = prefabs[prefab.assetId];
                Debug.LogWarning($"Replacing existing prefab with assetId '{prefab.assetId}'. Old prefab '{existingPrefab.name}', New prefab '{prefab.name}'");
            }

            // Debug.Log($"Registering prefab '{prefab.name}' as asset:{prefab.assetId}");

            prefabs[prefab.assetId] = prefab.gameObject;
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
                Debug.LogError("Could not register prefab because it was null");
                return;
            }

            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError($"Could not register '{prefab.name}' since it contains no NetworkIdentity component");
                return;
            }

            RegisterPrefabIdentity(identity);
        }

        /// <summary>
        /// Removes a registered spawn prefab that was setup with ClientScene.RegisterPrefab.
        /// </summary>
        /// <param name="prefab">The prefab to be removed from registration.</param>
        public static void UnregisterPrefab(GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogError("Could not unregister prefab because it was null");
                return;
            }

            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError("Could not unregister '" + prefab.name + "' since it contains no NetworkIdentity component");
                return;
            }

            Guid assetId = identity.assetId;

            prefabs.Remove(assetId);
        }

        /// <summary>
        /// This clears the registered spawn prefabs and spawn handler functions for this client.
        /// </summary>
        public static void ClearSpawners()
        {
            prefabs.Clear();
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
                        identity.OnStopClient();
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
                NetworkIdentity.spawned.Clear();
            }
            catch (InvalidOperationException e)
            {
                Debug.LogException(e);
                Debug.LogError("Could not DestroyAllClientObjects because spawned list was modified during loop, make sure you are not modifying NetworkIdentity.spawned by calling NetworkServer.Destroy or NetworkServer.Spawn in OnDestroy or OnDisable.");
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

            // call OnStartAuthority, OnStartClient, OnStartLocalPlayer
            // note: unlike UNET/original Mirror, we do NOT call those functions
            //       after ALL objects were spawned. with the new
            //       InterestManagementSystem, spawns/unspawns happen when they
            //       happen. there is no guaranteed order or time.
            //       in other words, we can't rely on another spawned object in
            //       OnStartClient/LocalPlayer/Authority anymore!
            identity.NotifyAuthority();
            identity.OnStartClient();
            CheckForLocalPlayer(identity);
        }

        internal static void OnSpawn(SpawnMessage msg)
        {
            // Debug.Log($"Client spawn handler instantiating netId={msg.netId} assetID={msg.assetId} sceneId={msg.sceneId:X} pos={msg.position}");

            if (FindOrSpawnObject(msg, out NetworkIdentity identity))
            {
                ApplySpawnPayload(identity, msg);
            }
        }

        /// <summary>
        /// Finds Existing Object with NetId or spawns a new one using AssetId or sceneId
        /// </summary>
        internal static bool FindOrSpawnObject(SpawnMessage msg, out NetworkIdentity identity)
        {
            // was the object already spawned?
            identity = GetExistingObject(msg.netId);

            // if found, return early
            if (identity != null)
            {
                return true;
            }

            if (msg.assetId == Guid.Empty && msg.sceneId == 0)
            {
                Debug.LogError($"OnSpawn message with netId '{msg.netId}' has no AssetId or sceneId");
                return false;
            }

            identity = msg.sceneId == 0 ? SpawnPrefab(msg) : SpawnSceneObject(msg);

            if (identity == null)
            {
                Debug.LogError($"Could not spawn assetId={msg.assetId} scene={msg.sceneId:X} netId={msg.netId}");
                return false;
            }

            return true;
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
                // Debug.Log("Client spawn handler instantiating [netId:" + msg.netId + " asset ID:" + msg.assetId + " pos:" + msg.position + " rotation: " + msg.rotation + "]");

                return obj.GetComponent<NetworkIdentity>();
            }
            Debug.LogError($"Failed to spawn server object, did you forget to add it to the NetworkManager? assetId={msg.assetId} netId={msg.netId}");
            return null;
        }

        static NetworkIdentity SpawnSceneObject(SpawnMessage msg)
        {
            NetworkIdentity identity = GetAndRemoveSceneObject(msg.sceneId);
            if (identity == null)
            {
                Debug.LogError($"Spawn scene object not found for {msg.sceneId:X} SpawnableObjects.Count={spawnableObjects.Count}");

                // dump the whole spawnable objects dict for easier debugging
                // foreach (KeyValuePair<ulong, NetworkIdentity> kvp in spawnableObjects)
                //     Debug.Log($"Spawnable: SceneId={kvp.Key:X} name={kvp.Value.name}");
            }
            else
            {
                // only log this when successful
                // Debug.Log($"Client spawn for [netId:{msg.netId}] [sceneId:{msg.sceneId:X}] obj:{identity}");
            }

            return identity;
        }

        static NetworkIdentity GetAndRemoveSceneObject(ulong sceneId)
        {
            if (spawnableObjects.TryGetValue(sceneId, out NetworkIdentity identity))
            {
                spawnableObjects.Remove(sceneId);
                return identity;
            }
            return null;
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
            // Debug.Log("ClientScene.OnObjDestroy netId:" + netId);

            if (NetworkIdentity.spawned.TryGetValue(netId, out NetworkIdentity localObject) && localObject != null)
            {
                localObject.OnStopClient();

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

                NetworkIdentity.spawned.Remove(netId);
                localObject.Reset();
            }
            else
            {
                // Debug.LogWarning("Did not find target for destroy message for " + netId);
            }
        }

        internal static void OnUpdateVarsMessage(UpdateVarsMessage msg)
        {
            // Debug.Log("ClientScene.OnUpdateVarsMessage " + msg.netId);

            if (NetworkIdentity.spawned.TryGetValue(msg.netId, out NetworkIdentity localObject) && localObject != null)
            {
                using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(msg.payload))
                    localObject.OnDeserializeAllSafely(networkReader, false);
            }
            else
            {
                Debug.LogWarning("Did not find target for sync message for " + msg.netId + " . Note: this can be completely normal because UDP messages may arrive out of order, so this message might have arrived after a Destroy message.");
            }
        }

        internal static void OnRPCMessage(RpcMessage msg)
        {
            // Debug.Log("ClientScene.OnRPCMessage hash:" + msg.functionHash + " netId:" + msg.netId);

            if (NetworkIdentity.spawned.TryGetValue(msg.netId, out NetworkIdentity identity))
            {
                using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(msg.payload))
                    identity.HandleRemoteCall(msg.componentIndex, msg.functionHash, MirrorInvokeType.ClientRpc, networkReader);
            }
        }

        static void CheckForLocalPlayer(NetworkIdentity identity)
        {
            if (identity == localPlayer)
            {
                // Set isLocalPlayer to true on this NetworkIdentity and trigger OnStartLocalPlayer in all scripts on the same GO
                identity.connectionToServer = readyConnection;
                identity.OnStartLocalPlayer();

                // Debug.Log("ClientScene.OnOwnerMessage - player=" + identity.name);
            }
        }
    }
}
