using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Mirror.RemoteCalls;
using UnityEngine;

namespace Mirror
{

    [AddComponentMenu("Network/ClientObjectManager")]
    [DisallowMultipleComponent]
    public class ClientObjectManager : MonoBehaviour
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(ClientObjectManager));

        public NetworkClient client;
        public NetworkSceneManager networkSceneManager;

        // spawn handlers. internal for testing purposes. do not use directly.
        internal readonly Dictionary<Guid, SpawnHandlerDelegate> spawnHandlers = new Dictionary<Guid, SpawnHandlerDelegate>();
        internal readonly Dictionary<Guid, UnSpawnDelegate> unspawnHandlers = new Dictionary<Guid, UnSpawnDelegate>();

        /// <summary>
        /// This is a dictionary of the prefabs that are registered on the client with ClientScene.RegisterPrefab().
        /// <para>The key to the dictionary is the prefab asset Id.</para>
        /// </summary>
        internal readonly Dictionary<Guid, NetworkIdentity> prefabs = new Dictionary<Guid, NetworkIdentity>();

        /// <summary>
        /// List of prefabs that will be registered with the spawning system.
        /// <para>For each of these prefabs, ClientManager.RegisterPrefab() will be automatically invoke.</para>
        /// </summary>
        public List<NetworkIdentity> spawnPrefabs = new List<NetworkIdentity>();

        /// <summary>
        /// This is dictionary of the disabled NetworkIdentity objects in the scene that could be spawned by messages from the server.
        /// <para>The key to the dictionary is the NetworkIdentity sceneId.</para>
        /// </summary>
        public readonly Dictionary<ulong, NetworkIdentity> spawnableObjects = new Dictionary<ulong, NetworkIdentity>();

        public void Start()
        {
            if (client != null)
            {
                client.Connected.AddListener(OnClientConnected);
                client.Disconnected.AddListener(OnClientDisconnected);

                if (networkSceneManager != null)
                    networkSceneManager.ClientSceneChanged.AddListener(OnClientSceneChanged);
            }
        }

        void OnClientConnected(INetworkConnection conn)
        {
            RegisterSpawnPrefabs();

            if(client.IsLocalClient)
            {
                RegisterHostHandlers();
            }
            else
            {
                RegisterMessageHandlers();
            }
        }

        void OnClientDisconnected()
        {
            ClearSpawners();
            DestroyAllClientObjects();
        }

        void OnClientSceneChanged(string scenePath, SceneOperation sceneOperation)
        {
            PrepareToSpawnSceneObjects();
        }

        internal void RegisterHostHandlers()
        {
            client.Connection.RegisterHandler<ObjectDestroyMessage>(OnHostClientObjectDestroy);
            client.Connection.RegisterHandler<ObjectHideMessage>(OnHostClientObjectHide);
            client.Connection.RegisterHandler<SpawnMessage>(OnHostClientSpawn);
            client.Connection.RegisterHandler<ServerRpcReply>(OnServerRpcReply);
            // host mode reuses objects in the server
            // so we don't need to spawn them
            client.Connection.RegisterHandler<UpdateVarsMessage>(msg => { });
            client.Connection.RegisterHandler<RpcMessage>(OnRpcMessage);
        }

        internal void RegisterMessageHandlers()
        {
            client.Connection.RegisterHandler<ObjectDestroyMessage>(OnObjectDestroy);
            client.Connection.RegisterHandler<ObjectHideMessage>(OnObjectHide);
            client.Connection.RegisterHandler<SpawnMessage>(OnSpawn);
            client.Connection.RegisterHandler<ServerRpcReply>(OnServerRpcReply);
            client.Connection.RegisterHandler<UpdateVarsMessage>(OnUpdateVarsMessage);
            client.Connection.RegisterHandler<RpcMessage>(OnRpcMessage);
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
            if (client.Connection != null)
            {
                client.Connection.Identity = identity;
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
                NetworkIdentity prefab = spawnPrefabs[i];
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
        /// <returns>true if prefab was registered</returns>
        public NetworkIdentity GetPrefab(Guid assetId)
        {
            if (assetId == Guid.Empty)
                return null;

            if (prefabs.TryGetValue(assetId, out NetworkIdentity identity))
            {
                return identity;
            }
            return null;
        }

        /// <summary>
        /// Registers a prefab with the spawning system.
        /// <para>When a NetworkIdentity object is spawned on a server with NetworkServer.SpawnObject(), and the prefab that the object was created from was registered with RegisterPrefab(), the client will use that prefab to instantiate a corresponding client object with the same netId.</para>
        /// <para>The ClientObjectManager has a list of spawnable prefabs, it uses this function to register those prefabs with the ClientScene.</para>
        /// <para>The set of current spawnable object is available in the ClientScene static member variable ClientScene.prefabs, which is a dictionary of NetworkAssetIds and prefab references.</para>
        /// </summary>
        /// <param name="identity">A Prefab that will be spawned.</param>
        /// <param name="newAssetId">An assetId to be assigned to this prefab. This allows a dynamically created game object to be registered for an already known asset Id.</param>
        public void RegisterPrefab(NetworkIdentity identity, Guid newAssetId)
        {
                identity.AssetId = newAssetId;

                if (logger.LogEnabled()) logger.Log("Registering prefab '" + identity.name + "' as asset:" + identity.AssetId);
                prefabs[identity.AssetId] = identity;
        }

        /// <summary>
        /// Registers a prefab with the spawning system.
        /// <para>When a NetworkIdentity object is spawned on a server with NetworkServer.SpawnObject(), and the prefab that the object was created from was registered with RegisterPrefab(), the client will use that prefab to instantiate a corresponding client object with the same netId.</para>
        /// <para>The ClientObjectManager has a list of spawnable prefabs, it uses this function to register those prefabs with the ClientScene.</para>
        /// <para>The set of current spawnable object is available in the ClientScene static member variable ClientScene.prefabs, which is a dictionary of NetworkAssetIds and prefab references.</para>
        /// </summary>
        /// <param name="identity">A Prefab that will be spawned.</param>
        public void RegisterPrefab(NetworkIdentity identity)
        {
            if (logger.LogEnabled()) logger.Log("Registering prefab '" + identity.name + "' as asset:" + identity.AssetId);
            prefabs[identity.AssetId] = identity;
        }

        /// <summary>
        /// Registers a prefab with the spawning system.
        /// <para>When a NetworkIdentity object is spawned on a server with NetworkServer.SpawnObject(), and the prefab that the object was created from was registered with RegisterPrefab(), the client will use that prefab to instantiate a corresponding client object with the same netId.</para>
        /// <para>The ClientObjectManager has a list of spawnable prefabs, it uses this function to register those prefabs with the ClientScene.</para>
        /// <para>The set of current spawnable object is available in the ClientScene static member variable ClientScene.prefabs, which is a dictionary of NetworkAssetIds and prefab references.</para>
        /// </summary>
        /// <param name="identity">A Prefab that will be spawned.</param>
        /// <param name="spawnHandler">A method to use as a custom spawnhandler on clients.</param>
        /// <param name="unspawnHandler">A method to use as a custom un-spawnhandler on clients.</param>
        public void RegisterPrefab(NetworkIdentity identity, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler)
        {
            if (identity.AssetId == Guid.Empty)
            {
                throw new InvalidOperationException("RegisterPrefab game object " + identity.name + " has no " + nameof(identity) + ". Use RegisterSpawnHandler() instead?");
            }

            if (logger.LogEnabled()) logger.Log("Registering custom prefab '" + identity.name + "' as asset:" + identity.AssetId + " " + spawnHandler.GetMethodName() + "/" + unspawnHandler.GetMethodName());

            spawnHandlers[identity.AssetId] = spawnHandler;
            unspawnHandlers[identity.AssetId] = unspawnHandler;
        }

        /// <summary>
        /// Removes a registered spawn prefab that was setup with ClientScene.RegisterPrefab.
        /// </summary>
        /// <param name="identity">The prefab to be removed from registration.</param>
        public void UnregisterPrefab(NetworkIdentity identity)
        {
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
                handler(identity);
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
            foreach (NetworkIdentity identity in client.Spawned.Values)
            {
                if (identity != null && identity.gameObject != null)
                {
                    UnSpawn(identity);
                }
            }
            client.Spawned.Clear();
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
            identity.Client = client;
            identity.ClientObjectManager = this;

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

            client.Spawned[msg.netId] = identity;

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
                //is the object on the prefab or scene object lists?
                identity = msg.sceneId == 0 ? SpawnPrefab(msg) : SpawnSceneObject(msg);
            }

            if (identity == null)
            {
                //object could not be found.
                throw new InvalidOperationException($"Could not spawn assetId={msg.assetId} scene={msg.sceneId} netId={msg.netId}");
            }

            ApplySpawnPayload(identity, msg);
        }

        NetworkIdentity GetExistingObject(uint netid)
        {
            client.Spawned.TryGetValue(netid, out NetworkIdentity localObject);
            return localObject;
        }

        NetworkIdentity SpawnPrefab(SpawnMessage msg)
        {
            if (spawnHandlers.TryGetValue(msg.assetId, out SpawnHandlerDelegate handler))
            {
                NetworkIdentity obj = handler(msg);
                if (obj == null)
                {
                    logger.LogWarning("Client spawn handler for " + msg.assetId + " returned null");
                    return null;
                }
                return obj;
            }
            NetworkIdentity prefab = GetPrefab(msg.assetId);
            if (!(prefab is null))
            {
                NetworkIdentity obj = Instantiate(prefab, msg.position, msg.rotation);
                if (logger.LogEnabled())
                {
                    logger.Log("Client spawn handler instantiating [netId:" + msg.netId + " asset ID:" + msg.assetId + " pos:" + msg.position + " rotation: " + msg.rotation + "]");
                }

                return obj;
            }
            logger.LogError("Failed to spawn server object, did you forget to add it to the ClientObjectManager? assetId=" + msg.assetId + " netId=" + msg.netId);
            return null;
        }

        internal NetworkIdentity SpawnSceneObject(SpawnMessage msg)
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

            if (client.Spawned.TryGetValue(netId, out NetworkIdentity localObject) && localObject != null)
            {
                UnSpawn(localObject);
                client.Spawned.Remove(netId);
            }
            else
            {
                logger.LogWarning("Did not find target for destroy message for " + netId);
            }
        }

        internal void OnHostClientObjectDestroy(ObjectDestroyMessage msg)
        {
            if (logger.LogEnabled()) logger.Log("ClientScene.OnLocalObjectObjDestroy netId:" + msg.netId);

            client.Spawned.Remove(msg.netId);
        }

        internal void OnHostClientObjectHide(ObjectHideMessage msg)
        {
            if (logger.LogEnabled()) logger.Log("ClientScene::OnLocalObjectObjHide netId:" + msg.netId);

            if (client.Spawned.TryGetValue(msg.netId, out NetworkIdentity localObject) && localObject != null)
            {
                localObject.OnSetHostVisibility(false);
            }
        }

        internal void OnHostClientSpawn(SpawnMessage msg)
        {
            if (client.Spawned.TryGetValue(msg.netId, out NetworkIdentity localObject) && localObject != null)
            {
                if (msg.isLocalPlayer)
                    InternalAddPlayer(localObject);

                localObject.Client = client;
                localObject.ClientObjectManager = this;
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

            if (client.Spawned.TryGetValue(msg.netId, out NetworkIdentity localObject) && localObject != null)
            {
                using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(msg.payload))
                    localObject.OnDeserializeAllSafely(networkReader, false);
            }
            else
            {
                if (logger.WarnEnabled()) logger.LogWarning("Did not find target for sync message for " + msg.netId + " . Note: this can be completely normal because UDP messages may arrive out of order, so this message might have arrived after a Destroy message.");
            }
        }

        internal void OnRpcMessage(RpcMessage msg)
        {
            if (logger.LogEnabled()) logger.Log("ClientScene.OnRPCMessage hash:" + msg.functionHash + " netId:" + msg.netId);

            Skeleton skeleton = RemoteCallHelper.GetSkeleton(msg.functionHash);

            if (skeleton.invokeType != MirrorInvokeType.ClientRpc)
            {
                throw new MethodInvocationException($"Invalid RPC call with id {msg.functionHash}");
            }
            if (client.Spawned.TryGetValue(msg.netId, out NetworkIdentity identity) && identity != null)
            {
                using (PooledNetworkReader networkReader = NetworkReaderPool.GetReader(msg.payload))
                    identity.HandleRemoteCall(skeleton, msg.componentIndex, networkReader);
            }
        }

        void CheckForLocalPlayer(NetworkIdentity identity)
        {
            if (identity && identity == client.LocalPlayer)
            {
                // Set isLocalPlayer to true on this NetworkIdentity and trigger OnStartLocalPlayer in all scripts on the same GO
                identity.ConnectionToServer = client.Connection;
                identity.StartLocalPlayer();

                if (logger.LogEnabled()) logger.Log("ClientScene.OnOwnerMessage - player=" + identity.name);
            }
        }

        private void OnServerRpcReply(INetworkConnection connection, ServerRpcReply reply)
        {
            // find the callback that was waiting for this and invoke it.
            if (callbacks.TryGetValue(reply.replyId, out Action<NetworkReader> action))
            {
                callbacks.Remove(replyId);
                using (PooledNetworkReader reader = NetworkReaderPool.GetReader(reply.payload))
                {
                    action(reader);
                }
            }
            else
            {
                throw new MethodAccessException("Received reply but no handler was registered");
            }
        }

        private readonly Dictionary<int, Action<NetworkReader>> callbacks = new Dictionary<int, Action<NetworkReader>>();
        private int replyId;

        /// <summary>
        /// Creates a task that waits for a reply from the server
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>the task that will be completed when the result is in, and the id to use in the request</returns>
        internal (UniTask<T> task, int replyId) CreateReplyTask<T>()
        {
            int newReplyId = replyId++;
            var completionSource = AutoResetUniTaskCompletionSource<T>.Create();
            void Callback(NetworkReader reader)
            {
                T result = reader.Read<T>();
                completionSource.TrySetResult(result);
            }

            callbacks.Add(newReplyId, Callback);
            return (completionSource.Task, newReplyId);
        }


    }
}

