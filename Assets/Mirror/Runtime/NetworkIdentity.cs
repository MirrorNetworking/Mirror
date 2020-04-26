using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#if UNITY_2018_3_OR_NEWER
using UnityEditor.Experimental.SceneManagement;
#endif
#endif

namespace Mirror
{
    /// <summary>
    /// The NetworkIdentity identifies objects across the network, between server and clients. Its primary data is a NetworkInstanceId which is allocated by the server and then set on clients. This is used in network communications to be able to lookup game objects on different machines.
    /// </summary>
    /// <remarks>
    /// <para>The NetworkIdentity is used to synchronize information in the object with the network. Only the server should create instances of objects which have NetworkIdentity as otherwise they will not be properly connected to the system.</para>
    /// <para>For complex objects with a hierarchy of subcomponents, the NetworkIdentity must be on the root of the hierarchy. It is not supported to have multiple NetworkIdentity components on subcomponents of a hierarchy.</para>
    /// <para>NetworkBehaviour scripts require a NetworkIdentity on the game object to be able to function.</para>
    /// <para>The NetworkIdentity manages the dirty state of the NetworkBehaviours of the object. When it discovers that NetworkBehaviours are dirty, it causes an update packet to be created and sent to clients.</para>
    /// <para>The flow for serialization updates managed by the NetworkIdentity is:</para>
    /// <para>* Each NetworkBehaviour has a dirty mask. This mask is available inside OnSerialize as syncVarDirtyBits</para>
    /// <para>* Each SyncVar in a NetworkBehaviour script is assigned a bit in the dirty mask.</para>
    /// <para>* Changing the value of SyncVars causes the bit for that SyncVar to be set in the dirty mask</para>
    /// <para>* Alternatively, calling SetDirtyBit() writes directly to the dirty mask</para>
    /// <para>* NetworkIdentity objects are checked on the server as part of it&apos;s update loop</para>
    /// <para>* If any NetworkBehaviours on a NetworkIdentity are dirty, then an UpdateVars packet is created for that object</para>
    /// <para>* The UpdateVars packet is populated by calling OnSerialize on each NetworkBehaviour on the object</para>
    /// <para>* NetworkBehaviours that are NOT dirty write a zero to the packet for their dirty bits</para>
    /// <para>* NetworkBehaviours that are dirty write their dirty mask, then the values for the SyncVars that have changed</para>
    /// <para>* If OnSerialize returns true for a NetworkBehaviour, the dirty mask is reset for that NetworkBehaviour, so it will not send again until its value changes.</para>
    /// <para>* The UpdateVars packet is sent to ready clients that are observing the object</para>
    /// <para>On the client:</para>
    /// <para>* an UpdateVars packet is received for an object</para>
    /// <para>* The OnDeserialize function is called for each NetworkBehaviour script on the object</para>
    /// <para>* Each NetworkBehaviour script on the object reads a dirty mask.</para>
    /// <para>* If the dirty mask for a NetworkBehaviour is zero, the OnDeserialize functions returns without reading any more</para>
    /// <para>* If the dirty mask is non-zero value, then the OnDeserialize function reads the values for the SyncVars that correspond to the dirty bits that are set</para>
    /// <para>* If there are SyncVar hook functions, those are invoked with the value read from the stream.</para>
    /// </remarks>
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkIdentity")]
    [HelpURL("https://mirror-networking.com/docs/Components/NetworkIdentity.html")]
    public sealed class NetworkIdentity : MonoBehaviour
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(NetworkIdentity));

        // configuration
        NetworkBehaviour[] networkBehavioursCache;

        /// <summary>
        /// Returns true if running as a client and this object was spawned by a server.
        /// </summary>
        public bool IsClient => Client != null && Client.Active && NetId != 0 && !serverOnly;

        /// <summary>
        /// Returns true if NetworkServer.active and server is not stopped.
        /// </summary>
        public bool IsServer => Server != null && Server.Active && NetId != 0;

        /// <summary>
        /// Returns true if we're on host mode.
        /// </summary>
        public bool IsLocalClient => Server != null && Server.LocalClientActive;

        /// <summary>
        /// This returns true if this object is the one that represents the player on the local machine.
        /// <para>This is set when the server has spawned an object for this particular client.</para>
        /// </summary>
        public bool IsLocalPlayer => Client != null && Client.LocalPlayer == this;

        /// <summary>
        /// This returns true if this object is the authoritative player object on the client.
        /// <para>This value is determined at runtime. For most objects, authority is held by the server.</para>
        /// <para>For objects that had their authority set by AssignClientAuthority on the server, this will be true on the client that owns the object. NOT on other clients.</para>
        /// </summary>
        public bool HasAuthority { get; internal set; }

        /// <summary>
        /// The set of network connections (players) that can see this object.
        /// <para>null until OnStartServer was called. this is necessary for SendTo* to work properly in server-only mode.</para>
        /// </summary>
        public readonly HashSet<INetworkConnection> observers = new HashSet<INetworkConnection>();

        /// <summary>
        /// Unique identifier for this particular object instance, used for tracking objects between networked clients and the server.
        /// <para>This is a unique identifier for this particular GameObject instance. Use it to track GameObjects between networked clients and the server.</para>
        /// </summary>
        public uint NetId { get; internal set; }

        /// <summary>
        /// A unique identifier for NetworkIdentity objects within a scene.
        /// <para>This is used for spawning scene objects on clients.</para>
        /// </summary>
        // persistent scene id <sceneHash/32,sceneId/32> (see AssignSceneID comments)
        [FormerlySerializedAs("m_SceneId"), HideInInspector]
        public ulong sceneId;

        /// <summary>
        /// Flag to make this object only exist when the game is running as a server (or host).
        /// </summary>
        [FormerlySerializedAs("m_ServerOnly")]
        public bool serverOnly;

        /// <summary>
        /// The NetworkServer associated with this NetworkIdentity.
        /// </summary>
        public NetworkServer Server { get; internal set; }

        /// <summary>
        /// The NetworkConnection associated with this NetworkIdentity. This is only valid for player objects on a local client.
        /// </summary>
        public INetworkConnection ConnectionToServer { get; internal set; }

        /// <summary>
        /// The NetworkClient associated with this NetworkIdentity.
        /// </summary>
        public NetworkClient Client { get; internal set; }

        INetworkConnection _connectionToClient;

        /// <summary>
        /// The NetworkConnection associated with this <see cref="NetworkIdentity">NetworkIdentity.</see> This is valid for player and other owned objects in the server.
        /// <para>Use it to return details such as the connection&apos;s identity, IP address and ready status.</para>
        /// </summary>
        public INetworkConnection ConnectionToClient
        {
            get => _connectionToClient;

            internal set
            {
                if (_connectionToClient != null)
                    _connectionToClient.RemoveOwnedObject(this);

                _connectionToClient = value;
                _connectionToClient?.AddOwnedObject(this);
            }
        }

        public NetworkBehaviour[] NetworkBehaviours
        {
            get
            {
                if (networkBehavioursCache != null)
                    return networkBehavioursCache;

                var components = GetComponents<NetworkBehaviour>();
                if (components.Length > 64)
                    throw new InvalidOperationException("Only 64 NetworkBehaviour per gameobject allowed");

                networkBehavioursCache = components;
                return networkBehavioursCache;
            }
        }


        // NetworkProximityChecker caching
        NetworkVisibility visibilityCache;
        public NetworkVisibility Visibility
        {
            get
            {
                if (visibilityCache == null)
                {
                    visibilityCache = GetComponent<NetworkVisibility>();
                }
                return visibilityCache;
            }
        }

        [SerializeField, HideInInspector] string m_AssetId;

        // the AssetId trick:
        // - ideally we would have a serialized 'Guid m_AssetId' but Unity can't
        //   serialize it because Guid's internal bytes are private
        // - UNET used 'NetworkHash128' originally, with byte0, ..., byte16
        //   which works, but it just unnecessary extra code
        // - using just the Guid string would work, but it's 32 chars long and
        //   would then be sent over the network as 64 instead of 16 bytes
        // -> the solution is to serialize the string internally here and then
        //    use the real 'Guid' type for everything else via .assetId
        /// <summary>
        /// Unique identifier used to find the source assets when server spawns the on clients.
        /// </summary>
        public Guid AssetId
        {
            get
            {
#if UNITY_EDITOR
                // This is important because sometimes OnValidate does not run (like when adding view to prefab with no child links)
                if (string.IsNullOrEmpty(m_AssetId))
                    SetupIDs();
#endif
                // convert string to Guid and use .Empty to avoid exception if
                // we would use 'new Guid("")'
                return string.IsNullOrEmpty(m_AssetId) ? Guid.Empty : new Guid(m_AssetId);
            }
            internal set
            {
                string newAssetIdString = value.ToString("N");
                if (string.IsNullOrEmpty(m_AssetId) || m_AssetId == newAssetIdString)
                {
                    m_AssetId = newAssetIdString;
                }
                else logger.LogWarning($"SetDynamicAssetId object {name} already has an assetId {m_AssetId}, new asset id {newAssetIdString}");
            }
        }

        // keep track of all sceneIds to detect scene duplicates
        static readonly Dictionary<ulong, NetworkIdentity> sceneIds = new Dictionary<ulong, NetworkIdentity>();

        /// <summary>
        /// This is invoked for NetworkBehaviour objects when they become active on the server.
        /// <para>This could be triggered by NetworkServer.Listen() for objects in the scene, or by NetworkServer.Spawn() for objects that are dynamically created.</para>
        /// <para>This will be called for objects on a "host" as well as for object on a dedicated server.</para>
        /// </summary>
        public UnityEvent OnStartServer = new UnityEvent();

        /// <summary>
        /// Called on every NetworkBehaviour when it is activated on a client.
        /// <para>Objects on the host have this function called, as there is a local client on the host. The values of SyncVars on object are guaranteed to be initialized correctly with the latest state from the server when this function is called on the client.</para>
        /// </summary>
        public UnityEvent OnStartClient = new UnityEvent();

        /// <summary>
        /// Called when the local player object has been set up.
        /// <para>This happens after OnStartClient(), as it is triggered by an ownership message from the server. This is an appropriate place to activate components or functionality that should only be active for the local player, such as cameras and input.</para>
        /// </summary>
        public UnityEvent OnStartLocalPlayer = new UnityEvent();

        /// <summary>
        /// This is invoked on behaviours that have authority, based on context and <see cref="NetworkIdentity.HasAuthority">NetworkIdentity.hasAuthority</see>.
        /// <para>This is called after <see cref="OnStartServer">OnStartServer</see> and before <see cref="OnStartClient">OnStartClient.</see></para>
        /// <para>When <see cref="NetworkIdentity.AssignClientAuthority"/> is called on the server, this will be called on the client that owns the object. When an object is spawned with <see cref="NetworkServer.Spawn">NetworkServer.Spawn</see> with a NetworkConnection parameter included, this will be called on the client that owns the object.</para>
        /// </summary>
        public UnityEvent OnStartAuthority = new UnityEvent();

        /// <summary>
        /// This is invoked on behaviours when authority is removed.
        /// <para>When NetworkIdentity.RemoveClientAuthority is called on the server, this will be called on the client that owns the object.</para>
        /// </summary>
        public UnityEvent OnStopAuthority = new UnityEvent();

        /// <summary>
        /// This is invoked on clients when the server has caused this object to be destroyed.
        /// <para>This can be used as a hook to invoke effects or do client specific cleanup.</para>
        /// </summary>
        ///<summary>Called on clients when the server destroys the GameObject.</summary>
        public UnityEvent OnStopClient = new UnityEvent();

        /// <summary>
        /// This is called on the server when the object is unspawned
        /// </summary>
        /// <remarks>Can be used as hook to save player information</remarks>
        public UnityEvent OnStopServer = new UnityEvent();

        /// <summary>
        /// Gets the NetworkIdentity from the sceneIds dictionary with the corresponding id
        /// </summary>
        /// <param name="id"></param>
        /// <returns>NetworkIdentity from the sceneIds dictionary</returns>
        public static NetworkIdentity GetSceneIdentity(ulong id) => sceneIds[id];

        // used when adding players
        internal void SetClientOwner(INetworkConnection conn)
        {
            // do nothing if it already has an owner
            if (ConnectionToClient != null && conn != ConnectionToClient)
            {
                throw new InvalidOperationException($"Object {this} netId={NetId} already has an owner. Use RemoveClientAuthority() first");
            }

            // otherwise set the owner connection
            ConnectionToClient = conn;
        }

        static uint nextNetworkId = 1;
        internal static uint GetNextNetworkId() => nextNetworkId++;

        /// <summary>
        /// Resets nextNetworkId = 1
        /// </summary>
        public static void ResetNextNetworkId() => nextNetworkId = 1;

        /// <summary>
        /// The delegate type for the clientAuthorityCallback.
        /// </summary>
        /// <param name="conn">The network connection that is gaining or losing authority.</param>
        /// <param name="identity">The object whose client authority status is being changed.</param>
        /// <param name="authorityState">The new state of client authority of the object for the connection.</param>
        public delegate void ClientAuthorityCallback(INetworkConnection conn, NetworkIdentity identity, bool authorityState);

        /// <summary>
        /// A callback that can be populated to be notified when the client-authority state of objects changes.
        /// <para>Whenever an object is spawned with client authority, or the client authority status of an object is changed with AssignClientAuthority or RemoveClientAuthority, then this callback will be invoked.</para>
        /// <para>This callback is only invoked on the server.</para>
        /// </summary>
        public static event ClientAuthorityCallback clientAuthorityCallback;

        // this is used when a connection is destroyed, since the "observers" property is read-only
        internal void RemoveObserverInternal(INetworkConnection conn)
        {
            observers.Remove(conn);
        }

        void Awake()
        {
            // detect runtime sceneId duplicates, e.g. if a user tries to
            // Instantiate a sceneId object at runtime. if we don't detect it,
            // then the client won't know which of the two objects to use for a
            // SpawnSceneObject message, and it's likely going to be the wrong
            // object.
            //
            // This might happen if for example we have a Dungeon GameObject
            // which contains a Skeleton monster as child, and when a player
            // runs into the Dungeon we create a Dungeon Instance of that
            // Dungeon, which would duplicate a scene object.
            //
            // see also: https://github.com/vis2k/Mirror/issues/384
            if (Application.isPlaying && sceneId != 0)
            {
                if (sceneIds.TryGetValue(sceneId, out NetworkIdentity existing) && existing != this)
                {
                    logger.LogError(name + "'s sceneId: " + sceneId.ToString("X") + " is already taken by: " + existing.name + ". Don't call Instantiate for NetworkIdentities that were in the scene since the beginning (aka scene objects). Otherwise the client won't know which object to use for a SpawnSceneObject message.");
                    Destroy(gameObject);
                }
                else
                {
                    sceneIds[sceneId] = this;
                }
            }
        }

        void OnValidate()
        {
#if UNITY_EDITOR
            SetupIDs();
#endif
        }

#if UNITY_EDITOR
        void AssignAssetID(GameObject prefab) => AssignAssetID(AssetDatabase.GetAssetPath(prefab));
        void AssignAssetID(string path) => m_AssetId = AssetDatabase.AssetPathToGUID(path);

        bool ThisIsAPrefab() => PrefabUtility.IsPartOfPrefabAsset(gameObject);

        bool ThisIsASceneObjectWithPrefabParent(out GameObject prefab)
        {
            prefab = null;

            if (!PrefabUtility.IsPartOfPrefabInstance(gameObject))
            {
                return false;
            }
            prefab = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);

            if (prefab == null)
            {
                logger.LogError("Failed to find prefab parent for scene object [name:" + gameObject.name + "]");
                return false;
            }
            return true;
        }

        static uint GetRandomUInt()
        {
            // use Crypto RNG to avoid having time based duplicates
            using (var rng = new RNGCryptoServiceProvider())
            {
                byte[] bytes = new byte[4];
                rng.GetBytes(bytes);
                return BitConverter.ToUInt32(bytes, 0);
            }
        }

        // persistent sceneId assignment
        // (because scene objects have no persistent unique ID in Unity)
        //
        // original UNET used OnPostProcessScene to assign an index based on
        // FindObjectOfType<NetworkIdentity> order.
        // -> this didn't work because FindObjectOfType order isn't deterministic.
        // -> one workaround is to sort them by sibling paths, but it can still
        //    get out of sync when we open scene2 in editor and we have
        //    DontDestroyOnLoad objects that messed with the sibling index.
        //
        // we absolutely need a persistent id. challenges:
        // * it needs to be 0 for prefabs
        //   => we set it to 0 in SetupIDs() if prefab!
        // * it needs to be only assigned in edit time, not at runtime because
        //   only the objects that were in the scene since beginning should have
        //   a scene id.
        //   => Application.isPlaying check solves that
        // * it needs to detect duplicated sceneIds after duplicating scene
        //   objects
        //   => sceneIds dict takes care of that
        // * duplicating the whole scene file shouldn't result in duplicate
        //   scene objects
        //   => buildIndex is shifted into sceneId for that.
        //   => if we have no scenes in build index then it doesn't matter
        //      because by definition a build can't switch to other scenes
        //   => if we do have scenes in build index then it will be != -1
        //   note: the duplicated scene still needs to be opened once for it to
        //          be set properly
        // * scene objects need the correct scene index byte even if the scene's
        //   build index was changed or a duplicated scene wasn't opened yet.
        //   => OnPostProcessScene is the only function that gets called for
        //      each scene before runtime, so this is where we set the scene
        //      byte.
        // * disabled scenes in build settings should result in same scene index
        //   in editor and in build
        //   => .gameObject.scene.buildIndex filters out disabled scenes by
        //      default
        // * generated sceneIds absolutely need to set scene dirty and force the
        //   user to resave.
        //   => Undo.RecordObject does that perfectly.
        // * sceneIds should never be generated temporarily for unopened scenes
        //   when building, otherwise editor and build get out of sync
        //   => BuildPipeline.isBuildingPlayer check solves that
        void AssignSceneID()
        {
            // we only ever assign sceneIds at edit time, never at runtime.
            // by definition, only the original scene objects should get one.
            // -> if we assign at runtime then server and client would generate
            //    different random numbers!
            if (Application.isPlaying)
                return;

            // no valid sceneId yet, or duplicate?
            bool duplicate = sceneIds.TryGetValue(sceneId, out NetworkIdentity existing) && existing != null && existing != this;
            if (sceneId == 0 || duplicate)
            {
                // clear in any case, because it might have been a duplicate
                sceneId = 0;

                // if a scene was never opened and we are building it, then a
                // sceneId would be assigned to build but not saved in editor,
                // resulting in them getting out of sync.
                // => don't ever assign temporary ids. they always need to be
                //    permanent
                // => throw an exception to cancel the build and let the user
                //    know how to fix it!
                if (BuildPipeline.isBuildingPlayer)
                    throw new InvalidOperationException("Scene " + gameObject.scene.path + " needs to be opened and resaved before building, because the scene object " + name + " has no valid sceneId yet.");

                // if we generate the sceneId then we MUST be sure to set dirty
                // in order to save the scene object properly. otherwise it
                // would be regenerated every time we reopen the scene, and
                // upgrading would be very difficult.
                // -> Undo.RecordObject is the new EditorUtility.SetDirty!
                // -> we need to call it before changing.
                Undo.RecordObject(this, "Generated SceneId");

                // generate random sceneId part (0x00000000FFFFFFFF)
                uint randomId = GetRandomUInt();

                // only assign if not a duplicate of an existing scene id
                // (small chance, but possible)
                duplicate = sceneIds.TryGetValue(randomId, out existing) && existing != null && existing != this;
                if (!duplicate)
                {
                    sceneId = randomId;
                }
            }

            // add to sceneIds dict no matter what
            // -> even if we didn't generate anything new, because we still need
            //    existing sceneIds in there to check duplicates
            sceneIds[sceneId] = this;
        }

        // copy scene path hash into sceneId for scene objects.
        // this is the only way for scene file duplication to not contain
        // duplicate sceneIds as it seems.
        // -> sceneId before: 0x00000000AABBCCDD
        // -> then we clear the left 4 bytes, so that our 'OR' uses 0x00000000
        // -> then we OR the hash into the 0x00000000 part
        // -> buildIndex is not enough, because Editor and Build have different
        //    build indices if there are disabled scenes in build settings, and
        //    if no scene is in build settings then Editor and Build have
        //    different indices too (Editor=0, Build=-1)
        // => ONLY USE THIS FROM POSTPROCESSSCENE!
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void SetSceneIdSceneHashPartInternal()
        {
            // get deterministic scene hash
            uint pathHash = (uint)gameObject.scene.path.GetStableHashCode();

            // shift hash from 0x000000FFFFFFFF to 0xFFFFFFFF00000000
            ulong shiftedHash = (ulong)pathHash << 32;

            // OR into scene id
            sceneId = (sceneId & 0xFFFFFFFF) | shiftedHash;

            // log it. this is incredibly useful to debug sceneId issues.
            if (logger.LogEnabled()) logger.Log(name + " in scene=" + gameObject.scene.name + " scene index hash(" + pathHash.ToString("X") + ") copied into sceneId: " + sceneId.ToString("X"));
        }

        void SetupIDs()
        {
            if (ThisIsAPrefab())
            {
                // force 0 for prefabs
                sceneId = 0;
                AssignAssetID(gameObject);
            }
            // are we currently in prefab editing mode? aka prefab stage
            // => check prefabstage BEFORE SceneObjectWithPrefabParent
            //    (fixes https://github.com/vis2k/Mirror/issues/976)
            // => if we don't check GetCurrentPrefabStage and only check
            //    GetPrefabStage(gameObject), then the 'else' case where we
            //    assign a sceneId and clear the assetId would still be
            //    triggered for prefabs. in other words: if we are in prefab
            //    stage, do not bother with anything else ever!
            else if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                // when modifying a prefab in prefab stage, Unity calls
                // OnValidate for that prefab and for all scene objects based on
                // that prefab.
                //
                // is this GameObject the prefab that we modify, and not just a
                // scene object based on the prefab?
                //   * GetCurrentPrefabStage = 'are we editing ANY prefab?'
                //   * GetPrefabStage(go) = 'are we editing THIS prefab?'
                if (PrefabStageUtility.GetPrefabStage(gameObject) != null)
                {
                    // force 0 for prefabs
                    sceneId = 0;
                    // NOTE: might make sense to use GetPrefabStage for asset
                    //       path, but let's not touch it while it works.
                    string path = PrefabStageUtility.GetCurrentPrefabStage().prefabAssetPath;
                    AssignAssetID(path);
                }
            }
            else if (ThisIsASceneObjectWithPrefabParent(out GameObject prefab))
            {
                AssignSceneID();
                AssignAssetID(prefab);
            }
            else
            {
                AssignSceneID();
                m_AssetId = "";
            }
        }
#endif

        // Unity will Destroy all networked objects on Scene Change, so we have to handle that here silently.
        // That means we cannot have any warning or logging in this method.
        void OnDestroy()
        {
            // remove from sceneIds
            // -> remove with (0xFFFFFFFFFFFFFFFF) and without (0x00000000FFFFFFFF)
            //    sceneHash to be 100% safe.
            sceneIds.Remove(sceneId);
            sceneIds.Remove(sceneId & 0x00000000FFFFFFFF);

            // If false the object has already been unspawned
            // if it is still true, then we need to unspawn it
            if (IsServer)
            {
                Server.Destroy(gameObject);
            }
        }

        internal void StartServer()
        {
            // do nothing if already spawned
            if (IsServer)
                return;

            // If the instance/net ID is invalid here then this is an object instantiated from a prefab and the server should assign a valid ID
            // NOTE: this might not be necessary because the above m_IsServer
            //       check already checks netId. BUT this case here checks only
            //       netId, so it would still check cases where isServer=false
            //       but netId!=0.
            if (NetId != 0)
            {
                // This object has already been spawned, this method might be called again
                // if we try to respawn all objects.  This can happen when we add a scene
                // in that case there is nothing else to do.
                return;
            }

            NetId = GetNextNetworkId();

            if (logger.LogEnabled()) logger.Log("OnStartServer " + this + " NetId:" + NetId + " SceneId:" + sceneId);

            // add to spawned (note: the original EnableIsServer isn't needed
            // because we already set m_isServer=true above)
            Server.spawned[NetId] = this;

            OnStartServer.Invoke();
        }

        internal void StopServer()
        {
            OnStopServer.Invoke();
        }

        bool clientStarted;
        internal void StartClient()
        {
            if (clientStarted)
                return;
            clientStarted = true;

            OnStartClient.Invoke();
        }

        bool localPlayerStarted;
        internal void StartLocalPlayer()
        {
            if (localPlayerStarted)
                return;
            localPlayerStarted = true;

            OnStartLocalPlayer.Invoke();
        }

        bool hadAuthority;
        internal void NotifyAuthority()
        {
            if (!hadAuthority && HasAuthority)
                StartAuthority();
            if (hadAuthority && !HasAuthority)
                StopAuthority();
            hadAuthority = HasAuthority;
        }

        internal void StartAuthority()
        {
            OnStartAuthority.Invoke();
        }

        internal void StopAuthority()
        {
            OnStopAuthority.Invoke();
        }

        internal void OnSetHostVisibility(bool visible)
        {
            if (Visibility != null)
            {
                Visibility.OnSetHostVisibility(visible);
            }
        }

        // check if observer can be seen by connection.
        // * returns true if seen.
        // * returns true if we have no proximity checker, so by default all are
        //   seen.
        internal bool OnCheckObserver(INetworkConnection conn)
        {
            if (Visibility != null)
            {
                return Visibility.OnCheckObserver(conn);
            }
            return true;
        }

        internal void StopClient()
        {
            OnStopClient.Invoke();
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        // random number that is unlikely to appear in a regular data stream
        const byte Barrier = 171;

        // paul: readstring bug prevention: https://issuetracker.unity3d.com/issues/unet-networkwriter-dot-write-causing-readstring-slash-readbytes-out-of-range-errors-in-clients
        // -> OnSerialize writes componentData, barrier, componentData, barrier,componentData,...
        // -> OnDeserialize carefully extracts each data, then deserializes the barrier and check it
        //    -> If we read too many or too few bytes,  the barrier is very unlikely to match
        //    -> we can properly track down errors
        void OnSerializeSafely(NetworkBehaviour comp, NetworkWriter writer, bool initialState)
        {
            bool result = comp.OnSerialize(writer, initialState);
            if (logger.LogEnabled()) logger.Log("OnSerializeSafely written for object=" + comp.name + " component=" + comp.GetType() + " sceneId=" + sceneId);

            // serialize a barrier to be checked by the deserializer
            writer.WriteByte(Barrier);
        }

        // serialize all components using dirtyComponentsMask
        // -> check ownerWritten/observersWritten to know if anything was written
        internal (int ownerWritten, int observersWritten) OnSerializeAllSafely(bool initialState, NetworkWriter ownerWriter, NetworkWriter observersWriter)
        {

            ulong dirtyComponentsMask = initialState ? GetIntialComponentsMask() : GetDirtyComponentsMask();

            // calculate syncMode mask at runtime. this allows users to change
            // component.syncMode while the game is running, which can be a huge
            // advantage over syncvar-based sync modes. e.g. if a player decides
            // to share or not share his inventory, or to go invisible, etc.
            //
            // (this also lets the TestSynchronizingObjects test pass because
            //  otherwise if we were to cache it in Awake, then we would call
            //  GetComponents<NetworkBehaviour> before all the test behaviours
            //  were added)
            ulong syncModeObserversMask = GetSyncModeObserversMask();

            int ownerWritten = 0;
            int observersWritten = 0;

            // write regular dirty mask for owner,
            // writer 'dirty mask & syncMode==Everyone' for everyone else
            // (WritePacked64 so we don't write full 8 bytes if we don't have to)
            ownerWriter.WritePackedUInt64(dirtyComponentsMask);
            observersWriter.WritePackedUInt64(dirtyComponentsMask & syncModeObserversMask);

            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                // is this component dirty?
                // -> always serialize if initialState so all components are included in spawn packet
                // -> note: IsDirty() is false if the component isn't dirty or sendInterval isn't elapsed yet
                if (initialState || comp.IsDirty())
                {
                    if (logger.LogEnabled()) logger.Log("OnSerializeAllSafely: " + name + " -> " + comp.GetType() + " initial=" + initialState);

                    // serialize into ownerWriter first
                    // (owner always gets everything!)
                    int startPosition = ownerWriter.Position;
                    OnSerializeSafely(comp, ownerWriter, initialState);
                    ++ownerWritten;

                    // copy into observersWriter too if SyncMode.Observers
                    // -> we copy instead of calling OnSerialize again because
                    //    we don't know what magic the user does in OnSerialize.
                    // -> it's not guaranteed that calling it twice gets the
                    //    same result
                    // -> it's not guaranteed that calling it twice doesn't mess
                    //    with the user's OnSerialize timing code etc.
                    // => so we just copy the result without touching
                    //    OnSerialize again
                    if (comp.syncMode == SyncMode.Observers)
                    {
                        var segment = ownerWriter.ToArraySegment();
                        int length = ownerWriter.Position - startPosition;
                        observersWriter.WriteBytes(segment.Array, startPosition, length);
                        ++observersWritten;
                    }
                }
            }

            return (ownerWritten, observersWritten);
        }

        private ulong GetDirtyComponentsMask()
        {
            // loop through all components only once and then write dirty+payload into the writer afterwards
            ulong dirtyComponentsMask = 0L;
            NetworkBehaviour[] components = NetworkBehaviours;
            for (int i = 0; i < components.Length; ++i)
            {
                NetworkBehaviour comp = components[i];
                if (comp.IsDirty())
                {
                    dirtyComponentsMask |= 1UL << i;
                }
            }

            return dirtyComponentsMask;
        }

        private ulong GetIntialComponentsMask()
        {
            // set a bit for every behaviour
            return NetworkBehaviours.Length == 64
                ? ulong.MaxValue
                : (1UL << NetworkBehaviours.Length) - 1;
        }

        // a mask that contains all the components with SyncMode.Observers
        internal ulong GetSyncModeObserversMask()
        {
            // loop through all components
            ulong mask = 0UL;
            NetworkBehaviour[] components = NetworkBehaviours;
            for (int i = 0; i < NetworkBehaviours.Length; ++i)
            {
                NetworkBehaviour comp = components[i];
                if (comp.syncMode == SyncMode.Observers)
                {
                    mask |= 1UL << i;
                }
            }

            return mask;
        }

        void OnDeserializeSafely(NetworkBehaviour comp, NetworkReader reader, bool initialState)
        {

            // call OnDeserialize with a temporary reader, so that the
            // original one can't be messed with. we also wrap it in a
            // try-catch block so there's no way to mess up another
            // component's deserialization

            comp.OnDeserialize(reader, initialState);

            byte barrierData = reader.ReadByte();
            if (barrierData != Barrier)
            {
                throw new InvalidMessageException("OnDeserialize failed for: object=" + name + " component=" + comp.GetType() + " sceneId=" + sceneId + ". Possible Reasons:\n  * Do " + comp.GetType() + "'s OnSerialize and OnDeserialize calls write the same amount of data? \n  * Was there an exception in " + comp.GetType() + "'s OnSerialize/OnDeserialize code?\n  * Are the server and client the exact same project?\n  * Maybe this OnDeserialize call was meant for another GameObject? The sceneIds can easily get out of sync if the Hierarchy was modified only in the client OR the server. Try rebuilding both.\n\n");
            }
        }

        internal void OnDeserializeAllSafely(NetworkReader reader, bool initialState)
        {
            // hack needed so that we can deserialize gameobjects and NI

            NetworkClient.Current = Client;
            // read component dirty mask
            ulong dirtyComponentsMask = reader.ReadPackedUInt64();

            NetworkBehaviour[] components = NetworkBehaviours;
            // loop through all components and deserialize the dirty ones
            for (int i = 0; i < components.Length; ++i)
            {
                // is the dirty bit at position 'i' set to 1?
                ulong dirtyBit = 1UL << i;
                if ((dirtyComponentsMask & dirtyBit) != 0L)
                {
                    OnDeserializeSafely(components[i], reader, initialState);
                }
            }
           
        }

        // helper function to handle SyncEvent/Command/Rpc
        void HandleRemoteCall(int componentIndex, int functionHash, MirrorInvokeType invokeType, NetworkReader reader)
        {
            if (gameObject == null)
            {
                logger.LogWarning(invokeType + " [" + functionHash + "] received for deleted object [netId=" + NetId + "]");
                return;
            }

            // hack sets the current client and server so that we can deserialize
            // gameobjects and network identities in the reader
            NetworkClient.Current = Client;
            NetworkServer.Current = Server;

            // find the right component to invoke the function on
            if (0 <= componentIndex && componentIndex < NetworkBehaviours.Length)
            {
                NetworkBehaviour invokeComponent = NetworkBehaviours[componentIndex];
                if (!invokeComponent.InvokeHandlerDelegate(functionHash, invokeType, reader))
                {
                    logger.LogError("Found no receiver for incoming " + invokeType + " [" + functionHash + "] on " + gameObject + ",  the server and client should have the same NetworkBehaviour instances [netId=" + NetId + "].");
                }
            }
            else
            {
                logger.LogWarning("Component [" + componentIndex + "] not found for [netId=" + NetId + "]");
            }
        }

        // happens on client
        internal void HandleSyncEvent(int componentIndex, int eventHash, NetworkReader reader)
        {
            HandleRemoteCall(componentIndex, eventHash, MirrorInvokeType.SyncEvent, reader);
        }

        // happens on server
        internal void HandleCommand(int componentIndex, int cmdHash, NetworkReader reader)
        {
            HandleRemoteCall(componentIndex, cmdHash, MirrorInvokeType.Command, reader);
        }

        // happens on client
        internal void HandleRpc(int componentIndex, int rpcHash, NetworkReader reader)
        {
            HandleRemoteCall(componentIndex, rpcHash, MirrorInvokeType.ClientRpc, reader);
        }

        internal void ClearObservers()
        {
            foreach (INetworkConnection conn in observers)
            {
                conn.RemoveFromVisList(this);
            }
            observers.Clear();
        }

        internal void AddObserver(INetworkConnection conn)
        {
            if (observers.Contains(conn))
            {
                // if we try to add a connectionId that was already added, then
                // we may have generated one that was already in use.
                return;
            }

            if (logger.LogEnabled()) logger.Log("Added observer " + conn.Address + " added for " + gameObject);
            observers.Add(conn);
            conn.AddToVisList(this);

            // spawn identity for this conn
            Server.ShowForConnection(this, conn);
        }

        // helper function to call OnRebuildObservers in all components
        // -> HashSet is passed in so we can cache it!
        // -> returns true if we have a proxchecker, false otherwise
        // -> initialize is true on first rebuild, false on consecutive rebuilds
        internal bool GetNewObservers(HashSet<INetworkConnection> observersSet, bool initialize)
        {
            observersSet.Clear();

            if (Visibility != null)
            {
                Visibility.OnRebuildObservers(observersSet, initialize);
                return true;
            }

            // we have no proximity checker. return false to indicate that we
            // should use the default implementation.
            return false;
        }

        // helper function to add all server connections as observers.
        // this is used if none of the components provides their own
        // OnRebuildObservers function.
        internal void AddAllReadyServerConnectionsToObservers()
        {
            // add all server connections
            foreach (INetworkConnection conn in Server.connections)
            {
                if (conn.IsReady)
                    AddObserver(conn);
            }

            // add local host connection (if any)
            if (Server.LocalConnection != null && Server.LocalConnection.IsReady)
            {
                AddObserver(Server.LocalConnection);
            }
        }

        static readonly HashSet<INetworkConnection> newObservers = new HashSet<INetworkConnection>();

        /// <summary>
        /// This causes the set of players that can see this object to be rebuild. The OnRebuildObservers callback function will be invoked on each NetworkBehaviour.
        /// </summary>
        /// <param name="initialize">True if this is the first time.</param>
        public void RebuildObservers(bool initialize)
        {
            bool changed = false;

            // call OnRebuildObservers function
            bool rebuildOverwritten = GetNewObservers(newObservers, initialize);

            // if player connection: ensure player always see himself no matter what.
            // -> fixes https://github.com/vis2k/Mirror/issues/692 where a
            //    player might teleport out of the ProximityChecker's cast,
            //    losing the own connection as observer.
            if (ConnectionToClient != null && ConnectionToClient.IsReady)
            {
                newObservers.Add(ConnectionToClient);
            }

            // if no component implemented OnRebuildObservers, then add all
            // server connections.
            if (!rebuildOverwritten)
            {
                // only add all connections when rebuilding the first time.
                // second time we just keep them without rebuilding anything.
                if (initialize)
                {
                    AddAllReadyServerConnectionsToObservers();
                }
                return;
            }

            // add all newObservers that aren't in .observers yet
            foreach (INetworkConnection conn in newObservers)
            {
                // only add ready connections.
                // otherwise the player might not be in the world yet or anymore
                if (conn != null && conn.IsReady)
                {
                    if (initialize || !observers.Contains(conn))
                    {
                        // new observer
                        conn.AddToVisList(this);
                        // spawn identity for this conn
                        Server.ShowForConnection(this, conn);
                        if (logger.LogEnabled()) logger.Log("New Observer for " + gameObject + " " + conn);
                        changed = true;
                    }
                }
            }

            // remove all old .observers that aren't in newObservers anymore
            foreach (INetworkConnection conn in observers)
            {
                if (!newObservers.Contains(conn))
                {
                    // removed observer
                    conn.RemoveFromVisList(this);
                    Server.HideForConnection(this, conn);

                    if (logger.LogEnabled()) logger.Log("Removed Observer for " + gameObject + " " + conn);
                    changed = true;
                }
            }

            if (changed)
            {
                observers.Clear();
                foreach (INetworkConnection conn in newObservers)
                {
                    if (conn != null && conn.IsReady)
                        observers.Add(conn);
                }
            }

            // special case for host mode: we use SetHostVisibility to hide
            // NetworkIdentities that aren't in observer range from host.
            // this is what games like Dota/Counter-Strike do too, where a host
            // does NOT see all players by default. they are in memory, but
            // hidden to the host player.
            //
            // this code is from UNET, it's a bit strange but it works:
            // * it hides newly connected identities in host mode
            //   => that part was the intended behaviour
            // * it hides ALL NetworkIdentities in host mode when the host
            //   connects but hasn't selected a character yet
            //   => this only works because we have no .localConnection != null
            //      check. at this stage, localConnection is null because
            //      StartHost starts the server first, then calls this code,
            //      then starts the client and sets .localConnection. so we can
            //      NOT add a null check without breaking host visibility here.
            // * it hides ALL NetworkIdentities in server-only mode because
            //   observers never contain the 'null' .localConnection
            //   => that was not intended, but let's keep it as it is so we
            //      don't break anything in host mode. it's way easier than
            //      iterating all identities in a special function in StartHost.
            if (initialize)
            {
                if (!newObservers.Contains(Server.LocalConnection))
                {
                    OnSetHostVisibility(false);
                }
            }
        }

        /// <summary>
        /// Assign control of an object to a client via the client's <see cref="NetworkConnection">NetworkConnection.</see>
        /// <para>This causes hasAuthority to be set on the client that owns the object, and NetworkBehaviour.OnStartAuthority will be called on that client. This object then will be in the NetworkConnection.clientOwnedObjects list for the connection.</para>
        /// <para>Authority can be removed with RemoveClientAuthority. Only one client can own an object at any time. This does not need to be called for player objects, as their authority is setup automatically.</para>
        /// </summary>
        /// <param name="conn">	The connection of the client to assign authority to.</param>
        public void AssignClientAuthority(INetworkConnection conn)
        {
            if (!IsServer)
            {
                throw new InvalidOperationException("AssignClientAuthority can only be called on the server for spawned objects");
            }

            if (conn == null)
            {
                throw new InvalidOperationException("AssignClientAuthority for " + gameObject + " owner cannot be null. Use RemoveClientAuthority() instead");
            }

            if (ConnectionToClient != null && conn != ConnectionToClient)
            {
                throw new InvalidOperationException("AssignClientAuthority for " + gameObject + " already has an owner. Use RemoveClientAuthority() first");
            }

            SetClientOwner(conn);

            // The client will match to the existing object
            // update all variables and assign authority
            Server.SendSpawnMessage(this, conn);

            clientAuthorityCallback?.Invoke(conn, this, true);
        }

        /// <summary>
        /// Removes ownership for an object.
        /// <para>This applies to objects that had authority set by AssignClientAuthority, or <see cref="NetworkServer.Spawn">NetworkServer.Spawn</see> with a NetworkConnection parameter included.</para>
        /// <para>Authority cannot be removed for player objects.</para>
        /// </summary>
        public void RemoveClientAuthority()
        {
            if (!IsServer)
            {
                throw new InvalidOperationException("RemoveClientAuthority can only be called on the server for spawned objects");
            }

            if (ConnectionToClient?.Identity == this)
            {
                throw new InvalidOperationException("RemoveClientAuthority cannot remove authority for a player object");
            }

            if (ConnectionToClient != null)
            {
                clientAuthorityCallback?.Invoke(ConnectionToClient, this, false);

                INetworkConnection previousOwner = ConnectionToClient;

                ConnectionToClient = null;

                // we need to resynchronize the entire object
                // so just spawn it again,
                // the client will not create a new instance,  it will simply
                // reset all variables and remove authority
                Server.SendSpawnMessage(this, previousOwner);
            }
        }

        // The object is no longer networked,  reset all network state
        internal void Reset()
        {
            ResetSyncObjects();
            clientStarted = false;
            localPlayerStarted = false;
            NetId = 0;
            Server = null;
            Client = null;
            ConnectionToServer = null;
            ConnectionToClient = null;
            networkBehavioursCache = null;

            ClearObservers();
        }

        // invoked by NetworkServer during Update()
        internal void ServerUpdate()
        {
            if (observers.Count > 0)
            {
                
                SendUpdateVarsMessage();
            }
            else
            {
                // clear all component's dirty bits.
                // it would be spawned on new observers anyway.
                ClearAllComponentsDirtyBits();
            }
        }

        void SendUpdateVarsMessage()
        {
            // one writer for owner, one for observers
            using (PooledNetworkWriter ownerWriter = NetworkWriterPool.GetWriter(), observersWriter = NetworkWriterPool.GetWriter())
            {
                // serialize all the dirty components and send
                (int ownerWritten, int observersWritten) = OnSerializeAllSafely(false, ownerWriter, observersWriter);
                if (ownerWritten > 0 || observersWritten > 0)
                {
                    UpdateVarsMessage varsMessage = new UpdateVarsMessage
                    {
                        netId = NetId
                    };

                    // send ownerWriter to owner
                    // (only if we serialized anything for owner)
                    // (only if there is a connection (e.g. if not a monster),
                    //  and if connection is ready because we use SendToReady
                    //  below too)
                    if (ownerWritten > 0)
                    {
                        varsMessage.payload = ownerWriter.ToArraySegment();
                        if (ConnectionToClient != null && ConnectionToClient.IsReady)
                            Server.SendToClientOfPlayer(this, varsMessage);
                    }

                    // send observersWriter to everyone but owner
                    // (only if we serialized anything for observers)
                    if (observersWritten > 0)
                    {
                        varsMessage.payload = observersWriter.ToArraySegment();
                        Server.SendToReady(this, varsMessage, false);
                    }

                    // clear dirty bits only for the components that we serialized
                    // DO NOT clean ALL component's dirty bits, because
                    // components can have different syncIntervals and we don't
                    // want to reset dirty bits for the ones that were not
                    // synced yet.
                    // (we serialized only the IsDirty() components, or all of
                    //  them if initialState. clearing the dirty ones is enough.)
                    ClearDirtyComponentsDirtyBits();
                }
            }
        }


        // clear all component's dirty bits no matter what
        internal void ClearAllComponentsDirtyBits()
        {
            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                comp.ClearAllDirtyBits();
            }
        }

        // clear only dirty component's dirty bits. ignores components which
        // may be dirty but not ready to be synced yet (because of syncInterval)
        internal void ClearDirtyComponentsDirtyBits()
        {
            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                if (comp.IsDirty())
                {
                    comp.ClearAllDirtyBits();
                }
            }
        }

        void ResetSyncObjects()
        {
            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                comp.ResetSyncObjects();
            }
        }
    }
}
