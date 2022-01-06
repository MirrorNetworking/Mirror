using System;
using System.Collections.Generic;
using Mirror.RemoteCalls;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
    using UnityEditor;

    #if UNITY_2021_2_OR_NEWER
        using UnityEditor.SceneManagement;
    #elif UNITY_2018_3_OR_NEWER
        using UnityEditor.Experimental.SceneManagement;
    #endif
#endif

namespace Mirror
{
    // Default = use interest management
    // ForceHidden = useful to hide monsters while they respawn etc.
    // ForceShown = useful to have score NetworkIdentities that always broadcast
    //              to everyone etc.
    public enum Visibility { Default, ForceHidden, ForceShown }

    public struct NetworkIdentitySerialization
    {
        // IMPORTANT: int tick avoids floating point inaccuracy over days/weeks
        public int tick;
        public NetworkWriter ownerWriter;
        public NetworkWriter observersWriter;
    }

    /// <summary>NetworkIdentity identifies objects across the network.</summary>
    [DisallowMultipleComponent]
    // NetworkIdentity.Awake initializes all NetworkComponents.
    // let's make sure it's always called before their Awake's.
    [DefaultExecutionOrder(-1)]
    [AddComponentMenu("Network/NetworkIdentity")]
    [HelpURL("https://mirror-networking.gitbook.io/docs/components/network-identity")]
    public sealed class NetworkIdentity : MonoBehaviour
    {
        /// <summary>Returns true if running as a client and this object was spawned by a server.</summary>
        //
        // IMPORTANT:
        //   OnStartClient sets it to true. we NEVER set it to false after.
        //   otherwise components like Skillbars couldn't use OnDestroy()
        //   for saving, etc. since isClient may have been reset before
        //   OnDestroy was called.
        //
        //   we also DO NOT make it dependent on NetworkClient.active or similar.
        //   we set it, then never change it. that's the user's expectation too.
        //
        //   => fixes https://github.com/vis2k/Mirror/issues/1475
        public bool isClient { get; internal set; }

        /// <summary>Returns true if NetworkServer.active and server is not stopped.</summary>
        //
        // IMPORTANT:
        //   OnStartServer sets it to true. we NEVER set it to false after.
        //   otherwise components like Skillbars couldn't use OnDestroy()
        //   for saving, etc. since isServer may have been reset before
        //   OnDestroy was called.
        //
        //   we also DO NOT make it dependent on NetworkServer.active or similar.
        //   we set it, then never change it. that's the user's expectation too.
        //
        //   => fixes https://github.com/vis2k/Mirror/issues/1484
        //   => fixes https://github.com/vis2k/Mirror/issues/2533
        public bool isServer { get; internal set; }

        /// <summary>Return true if this object represents the player on the local machine.</summary>
        //
        // IMPORTANT:
        //   OnStartLocalPlayer sets it to true. we NEVER set it to false after.
        //   otherwise components like Skillbars couldn't use OnDestroy()
        //   for saving, etc. since isLocalPlayer may have been reset before
        //   OnDestroy was called.
        //
        //   we also DO NOT make it dependent on NetworkClient.localPlayer or similar.
        //   we set it, then never change it. that's the user's expectation too.
        //
        //   => fixes https://github.com/vis2k/Mirror/issues/2615
        public bool isLocalPlayer { get; internal set; }

        /// <summary>True if this object only exists on the server</summary>
        public bool isServerOnly => isServer && !isClient;

        /// <summary>True if this object exists on a client that is not also acting as a server.</summary>
        public bool isClientOnly => isClient && !isServer;

        /// <summary>True if this object is the authoritative player object on the client.</summary>
        // Determined at runtime. For most objects, authority is held by the server.
        // For objects that had their authority set by AssignClientAuthority on
        // the server, this will be true on the client that owns the object. NOT
        // on other clients.
        public bool hasAuthority { get; internal set; }

        /// <summary>The set of network connections (players) that can see this object.</summary>
        // note: null until OnStartServer was called. this is necessary for
        //       SendTo* to work properly in server-only mode.
        public Dictionary<int, NetworkConnection> observers;

        /// <summary>The unique network Id of this object (unique at runtime).</summary>
        public uint netId { get; internal set; }

        /// <summary>Unique identifier for NetworkIdentity objects within a scene, used for spawning scene objects.</summary>
        // persistent scene id <sceneHash/32,sceneId/32> (see AssignSceneID comments)
        [FormerlySerializedAs("m_SceneId"), HideInInspector]
        public ulong sceneId;

        /// <summary>Make this object only exist when the game is running as a server (or host).</summary>
        [FormerlySerializedAs("m_ServerOnly")]
        [Tooltip("Prevents this object from being spawned / enabled on clients")]
        public bool serverOnly;

        // Set before Destroy is called so that OnDestroy doesn't try to destroy
        // the object again
        internal bool destroyCalled;

        /// <summary>Client's network connection to the server. This is only valid for player objects on the client.</summary>
        public NetworkConnection connectionToServer { get; internal set; }

        /// <summary>Server's network connection to the client. This is only valid for client-owned objects (including the Player object) on the server.</summary>
        public NetworkConnectionToClient connectionToClient
        {
            get => _connectionToClient;
            internal set
            {
                _connectionToClient?.RemoveOwnedObject(this);
                _connectionToClient = value;
                _connectionToClient?.AddOwnedObject(this);
            }
        }
        NetworkConnectionToClient _connectionToClient;

        /// <summary>All spawned NetworkIdentities by netId. Available on server and client.</summary>
        // server sees ALL spawned ones.
        // client sees OBSERVED spawned ones.
        // => split into NetworkServer.spawned and NetworkClient.spawned to
        //    reduce shared state between server & client.
        // => prepares for NetworkServer/Client as component & better host mode.
        [Obsolete("NetworkIdentity.spawned is now NetworkServer.spawned on server, NetworkClient.spawned on client.\nPrepares for NetworkServer/Client as component, better host mode, better testing.")]
        public static Dictionary<uint, NetworkIdentity> spawned
        {
            get
            {
                // server / host mode: use the one from server.
                // host mode has access to all spawned.
                if (NetworkServer.active) return NetworkServer.spawned;

                // client
                if (NetworkClient.active) return NetworkClient.spawned;

                // neither: then we are testing.
                // we could default to NetworkServer.spawned.
                // but from the outside, that's not obvious.
                // better to throw an exception to make it obvious.
                throw new Exception("NetworkIdentity.spawned was accessed before NetworkServer/NetworkClient were active.");
            }
        }

        // get all NetworkBehaviour components
        public NetworkBehaviour[] NetworkBehaviours { get; private set; }

        // current visibility
        //
        // Default = use interest management
        // ForceHidden = useful to hide monsters while they respawn etc.
        // ForceShown = useful to have score NetworkIdentities that always broadcast
        //              to everyone etc.
        //
        // TODO rename to 'visibility' after removing .visibility some day!
        [Tooltip("Visibility can overwrite interest management. ForceHidden can be useful to hide monsters while they respawn. ForceShown can be useful for score NetworkIdentities that should always broadcast to everyone in the world.")]
        public Visibility visible = Visibility.Default;

        // broadcasting serializes all entities around a player for each player.
        // we don't want to serialize one entity twice in the same tick.
        // so we cache the last serialization and remember the timestamp so we
        // know which Update it was serialized.
        // (timestamp is the same while inside Update)
        // => this way we don't need to pool thousands of writers either.
        // => way easier to store them per object
        NetworkIdentitySerialization lastSerialization = new NetworkIdentitySerialization
        {
            ownerWriter = new NetworkWriter(),
            observersWriter = new NetworkWriter()
        };

        /// <summary>Prefab GUID used to spawn prefabs across the network.</summary>
        //
        // The AssetId trick:
        //   Ideally we would have a serialized 'Guid m_AssetId' but Unity can't
        //   serialize it because Guid's internal bytes are private
        //
        //   UNET used 'NetworkHash128' originally, with byte0, ..., byte16
        //   which works, but it just unnecessary extra code
        //
        //   Using just the Guid string would work, but it's 32 chars long and
        //   would then be sent over the network as 64 instead of 16 bytes
        //
        // => The solution is to serialize the string internally here and then
        //    use the real 'Guid' type for everything else via .assetId
        public Guid assetId
        {
            get
            {
#if UNITY_EDITOR
                // This is important because sometimes OnValidate does not run (like when adding view to prefab with no child links)
                if (string.IsNullOrWhiteSpace(m_AssetId))
                    SetupIDs();
#endif
                // convert string to Guid and use .Empty to avoid exception if
                // we would use 'new Guid("")'
                return string.IsNullOrWhiteSpace(m_AssetId) ? Guid.Empty : new Guid(m_AssetId);
            }
            internal set
            {
                string newAssetIdString = value == Guid.Empty ? string.Empty : value.ToString("N");
                string oldAssetIdString = m_AssetId;

                // they are the same, do nothing
                if (oldAssetIdString == newAssetIdString)
                {
                    return;
                }

                // new is empty
                if (string.IsNullOrWhiteSpace(newAssetIdString))
                {
                    Debug.LogError($"Can not set AssetId to empty guid on NetworkIdentity '{name}', old assetId '{oldAssetIdString}'");
                    return;
                }

                // old not empty
                if (!string.IsNullOrWhiteSpace(oldAssetIdString))
                {
                    Debug.LogError($"Can not Set AssetId on NetworkIdentity '{name}' because it already had an assetId, current assetId '{oldAssetIdString}', attempted new assetId '{newAssetIdString}'");
                    return;
                }

                // old is empty
                m_AssetId = newAssetIdString;
                // Debug.Log($"Settings AssetId on NetworkIdentity '{name}', new assetId '{newAssetIdString}'");
            }
        }
        [SerializeField, HideInInspector] string m_AssetId;

        // Keep track of all sceneIds to detect scene duplicates
        static readonly Dictionary<ulong, NetworkIdentity> sceneIds =
            new Dictionary<ulong, NetworkIdentity>();

        // RuntimeInitializeOnLoadMethod -> fast playmode without domain reload
        // internal so it can be called from NetworkServer & NetworkClient
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void ResetStatics()
        {
            nextNetworkId = 1;
            clientAuthorityCallback = null;
            previousLocalPlayer = null;
        }

        /// <summary>Gets the NetworkIdentity from the sceneIds dictionary with the corresponding id</summary>
        public static NetworkIdentity GetSceneIdentity(ulong id) => sceneIds[id];

        // used when adding players
        internal void SetClientOwner(NetworkConnection conn)
        {
            // do nothing if it already has an owner
            if (connectionToClient != null && conn != connectionToClient)
            {
                Debug.LogError($"Object {this} netId={netId} already has an owner. Use RemoveClientAuthority() first", this);
                return;
            }

            // otherwise set the owner connection
            connectionToClient = (NetworkConnectionToClient)conn;
        }

        static uint nextNetworkId = 1;
        internal static uint GetNextNetworkId() => nextNetworkId++;

        /// <summary>Resets nextNetworkId = 1</summary>
        public static void ResetNextNetworkId() => nextNetworkId = 1;

        /// <summary>The delegate type for the clientAuthorityCallback.</summary>
        public delegate void ClientAuthorityCallback(NetworkConnection conn, NetworkIdentity identity, bool authorityState);

        /// <summary> A callback that can be populated to be notified when the client-authority state of objects changes.</summary>
        public static event ClientAuthorityCallback clientAuthorityCallback;

        // hasSpawned should always be false before runtime
        [SerializeField, HideInInspector] bool hasSpawned;
        public bool SpawnedFromInstantiate { get; private set; }

        // NetworkBehaviour components are initialized in Awake once.
        // Changing them at runtime would get client & server out of sync.
        // BUT internal so tests can add them after creating the NetworkIdentity
        internal void InitializeNetworkBehaviours()
        {
            // Get all NetworkBehaviours
            // (never null. GetComponents returns [] if none found)
            NetworkBehaviours = GetComponents<NetworkBehaviour>();
            if (NetworkBehaviours.Length > byte.MaxValue)
                Debug.LogError($"Only {byte.MaxValue} NetworkBehaviour components are allowed for NetworkIdentity: {name} because we send the index as byte.", this);

            // initialize each one
            for (int i = 0; i < NetworkBehaviours.Length; ++i)
            {
                NetworkBehaviour component = NetworkBehaviours[i];
                component.netIdentity = this;
                component.ComponentIndex = i;
            }
        }

        // Awake is only called in Play mode.
        // internal so we can call it during unit tests too.
        internal void Awake()
        {
            // initialize NetworkBehaviour components.
            // Awake() is called immediately after initialization.
            // no one can overwrite it because NetworkIdentity is sealed.
            // => doing it here is the fastest and easiest solution.
            InitializeNetworkBehaviours();

            if (hasSpawned)
            {
                Debug.LogError($"{name} has already spawned. Don't call Instantiate for NetworkIdentities that were in the scene since the beginning (aka scene objects).  Otherwise the client won't know which object to use for a SpawnSceneObject message.");
                SpawnedFromInstantiate = true;
                Destroy(gameObject);
            }
            hasSpawned = true;
        }

        void OnValidate()
        {
            // OnValidate is not called when using Instantiate, so we can use
            // it to make sure that hasSpawned is false
            hasSpawned = false;

#if UNITY_EDITOR
            SetupIDs();
#endif
        }

#if UNITY_EDITOR
        void AssignAssetID(string path)
        {
            // only set if not empty. fixes https://github.com/vis2k/Mirror/issues/2765
            if (!string.IsNullOrWhiteSpace(path))
                m_AssetId = AssetDatabase.AssetPathToGUID(path);
        }

        void AssignAssetID(GameObject prefab) => AssignAssetID(AssetDatabase.GetAssetPath(prefab));

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
                    throw new InvalidOperationException($"Scene {gameObject.scene.path} needs to be opened and resaved before building, because the scene object {name} has no valid sceneId yet.");

                // if we generate the sceneId then we MUST be sure to set dirty
                // in order to save the scene object properly. otherwise it
                // would be regenerated every time we reopen the scene, and
                // upgrading would be very difficult.
                // -> Undo.RecordObject is the new EditorUtility.SetDirty!
                // -> we need to call it before changing.
                Undo.RecordObject(this, "Generated SceneId");

                // generate random sceneId part (0x00000000FFFFFFFF)
                uint randomId = Utils.GetTrueRandomUInt();

                // only assign if not a duplicate of an existing scene id
                // (small chance, but possible)
                duplicate = sceneIds.TryGetValue(randomId, out existing) && existing != null && existing != this;
                if (!duplicate)
                {
                    sceneId = randomId;
                    //Debug.Log($"{name} in scene {gameObject.scene.name} sceneId assigned to:{sceneId:X}");
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
        public void SetSceneIdSceneHashPartInternal()
        {
            // Use `ToLower` to that because BuildPipeline.BuildPlayer is case insensitive but hash is case sensitive
            // If the scene in the project is `forest.unity` but `Forest.unity` is given to BuildPipeline then the
            // BuildPipeline will use `Forest.unity` for the build and create a different hash than the editor will.
            // Using ToLower will mean the hash will be the same for these 2 paths
            // Assets/Scenes/Forest.unity
            // Assets/Scenes/forest.unity
            string scenePath = gameObject.scene.path.ToLower();

            // get deterministic scene hash
            uint pathHash = (uint)scenePath.GetStableHashCode();

            // shift hash from 0x000000FFFFFFFF to 0xFFFFFFFF00000000
            ulong shiftedHash = (ulong)pathHash << 32;

            // OR into scene id
            sceneId = (sceneId & 0xFFFFFFFF) | shiftedHash;

            // log it. this is incredibly useful to debug sceneId issues.
            //Debug.Log($"{name} in scene {gameObject.scene.name} scene index hash {pathHash:X} copied into sceneId {sceneId:X}");
        }

        void SetupIDs()
        {
            // is this a prefab?
            if (Utils.IsPrefab(gameObject))
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
                    //Debug.Log($"{name} scene:{gameObject.scene.name} sceneid reset to 0 because CurrentPrefabStage={PrefabStageUtility.GetCurrentPrefabStage()} PrefabStage={PrefabStageUtility.GetPrefabStage(gameObject)}");

                    // get path from PrefabStage for this prefab
#if UNITY_2020_1_OR_NEWER
                    string path = PrefabStageUtility.GetPrefabStage(gameObject).assetPath;
#else
                    string path = PrefabStageUtility.GetPrefabStage(gameObject).prefabAssetPath;
#endif

                    AssignAssetID(path);
                }
            }
            // is this a scene object with prefab parent?
            else if (Utils.IsSceneObjectWithPrefabParent(gameObject, out GameObject prefab))
            {
                AssignSceneID();
                AssignAssetID(prefab);
            }
            else
            {
                AssignSceneID();

                // IMPORTANT: DO NOT clear assetId at runtime!
                // => fixes a bug where clicking any of the NetworkIdentity
                //    properties (like ServerOnly/ForceHidden) at runtime would
                //    call OnValidate
                // => OnValidate gets into this else case here because prefab
                //    connection isn't known at runtime
                // => then we would clear the previously assigned assetId
                // => and NetworkIdentity couldn't be spawned on other clients
                //    anymore because assetId was cleared
                if (!EditorApplication.isPlaying)
                {
                    m_AssetId = "";
                }
                // don't log. would show a lot when pressing play in uMMORPG/uSurvival/etc.
                //else Debug.Log($"Avoided clearing assetId at runtime for {name} after (probably) clicking any of the NetworkIdentity properties.");
            }
        }
#endif

        // OnDestroy is called for all SPAWNED NetworkIdentities
        // => scene objects aren't destroyed. it's not called for them.
        //
        // Note: Unity will Destroy all networked objects on Scene Change, so we
        // have to handle that here silently. That means we cannot have any
        // warning or logging in this method.
        void OnDestroy()
        {
            // Objects spawned from Instantiate are not allowed so are destroyed right away
            // we don't want to call NetworkServer.Destroy if this is the case
            if (SpawnedFromInstantiate)
                return;

            // If false the object has already been unspawned
            // if it is still true, then we need to unspawn it
            // if destroy is already called don't call it again
            if (isServer && !destroyCalled)
            {
                // Do not add logging to this (see above)
                NetworkServer.Destroy(gameObject);
            }

            if (isLocalPlayer)
            {
                // previously there was a bug where isLocalPlayer was
                // false in OnDestroy because it was dynamically defined as:
                //   isLocalPlayer => NetworkClient.localPlayer == this
                // we fixed it by setting isLocalPlayer manually and never
                // resetting it.
                //
                // BUT now we need to be aware of a possible data race like in
                // our rooms example:
                // => GamePlayer is in world
                // => player returns to room
                // => GamePlayer is destroyed
                // => NetworkClient.localPlayer is set to RoomPlayer
                // => GamePlayer.OnDestroy is called 1 frame later
                // => GamePlayer.OnDestroy 'isLocalPlayer' is true, so here we
                //    are trying to clear NetworkClient.localPlayer
                // => which would overwrite the new RoomPlayer local player
                //
                // FIXED by simply only clearing if NetworkClient.localPlayer
                // still points to US!
                // => see also: https://github.com/vis2k/Mirror/issues/2635
                if (NetworkClient.localPlayer == this)
                    NetworkClient.localPlayer = null;
            }
        }

        internal void OnStartServer()
        {
            // do nothing if already spawned
            if (isServer)
                return;

            // set isServer flag
            isServer = true;

            // set isLocalPlayer earlier, in case OnStartLocalplayer is called
            // AFTER OnStartClient, in which case it would still be falsse here.
            // many projects will check isLocalPlayer in OnStartClient though.
            // TODO ideally set isLocalPlayer when NetworkClient.localPlayer is set?
            if (NetworkClient.localPlayer == this)
            {
                isLocalPlayer = true;
            }

            // If the instance/net ID is invalid here then this is an object instantiated from a prefab and the server should assign a valid ID
            // NOTE: this might not be necessary because the above m_IsServer
            //       check already checks netId. BUT this case here checks only
            //       netId, so it would still check cases where isServer=false
            //       but netId!=0.
            if (netId != 0)
            {
                // This object has already been spawned, this method might be called again
                // if we try to respawn all objects.  This can happen when we add a scene
                // in that case there is nothing else to do.
                return;
            }

            netId = GetNextNetworkId();
            observers = new Dictionary<int, NetworkConnection>();

            //Debug.Log($"OnStartServer {this} NetId:{netId} SceneId:{sceneId:X}");

            // add to spawned (note: the original EnableIsServer isn't needed
            // because we already set m_isServer=true above)
            NetworkServer.spawned[netId] = this;

            // in host mode we set isClient true before calling OnStartServer,
            // otherwise isClient is false in OnStartServer.
            if (NetworkClient.active)
            {
                isClient = true;
            }

            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                // an exception in OnStartServer should be caught, so that one
                // component's exception doesn't stop all other components from
                // being initialized
                // => this is what Unity does for Start() etc. too.
                //    one exception doesn't stop all the other Start() calls!
                try
                {
                    comp.OnStartServer();
                }
                catch (Exception e)
                {
                    Debug.LogException(e, comp);
                }
            }
        }

        internal void OnStopServer()
        {
            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                // an exception in OnStartServer should be caught, so that one
                // component's exception doesn't stop all other components from
                // being initialized
                // => this is what Unity does for Start() etc. too.
                //    one exception doesn't stop all the other Start() calls!
                try
                {
                    comp.OnStopServer();
                }
                catch (Exception e)
                {
                    Debug.LogException(e, comp);
                }
            }
        }

        bool clientStarted;
        internal void OnStartClient()
        {
            if (clientStarted)
                return;
            clientStarted = true;

            isClient = true;

            // set isLocalPlayer earlier, in case OnStartLocalplayer is called
            // AFTER OnStartClient, in which case it would still be falsse here.
            // many projects will check isLocalPlayer in OnStartClient though.
            // TODO ideally set isLocalPlayer when NetworkClient.localPlayer is set?
            if (NetworkClient.localPlayer == this)
            {
                isLocalPlayer = true;
            }

            // Debug.Log($"OnStartClient {gameObject} netId:{netId}");
            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                // an exception in OnStartClient should be caught, so that one
                // component's exception doesn't stop all other components from
                // being initialized
                // => this is what Unity does for Start() etc. too.
                //    one exception doesn't stop all the other Start() calls!
                try
                {
                    // user implemented startup
                    comp.OnStartClient();
                }
                catch (Exception e)
                {
                    Debug.LogException(e, comp);
                }
            }
        }

        internal void OnStopClient()
        {
            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                // an exception in OnStopClient should be caught, so that
                // one component's exception doesn't stop all other components
                // from being initialized
                // => this is what Unity does for Start() etc. too.
                //    one exception doesn't stop all the other Start() calls!
                try
                {
                    comp.OnStopClient();
                }
                catch (Exception e)
                {
                    Debug.LogException(e, comp);
                }
            }
        }

        // TODO any way to make this not static?
        // introduced in https://github.com/vis2k/Mirror/commit/c7530894788bb843b0f424e8f25029efce72d8ca#diff-dc8b7a5a67840f75ccc884c91b9eb76ab7311c9ca4360885a7e41d980865bdc2
        // for PR https://github.com/vis2k/Mirror/pull/1263
        //
        // explanation:
        // we send the spawn message multiple times. Whenever an object changes
        // authority, we send the spawn message again for the object. This is
        // necessary because we need to reinitialize all variables when
        // ownership change due to sync to owner feature.
        // Without this static, the second time we get the spawn message we
        // would call OnStartLocalPlayer again on the same object
        static NetworkIdentity previousLocalPlayer = null;
        internal void OnStartLocalPlayer()
        {
            if (previousLocalPlayer == this)
                return;
            previousLocalPlayer = this;

            isLocalPlayer = true;

            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                // an exception in OnStartLocalPlayer should be caught, so that
                // one component's exception doesn't stop all other components
                // from being initialized
                // => this is what Unity does for Start() etc. too.
                //    one exception doesn't stop all the other Start() calls!
                try
                {
                    comp.OnStartLocalPlayer();
                }
                catch (Exception e)
                {
                    Debug.LogException(e, comp);
                }
            }
        }

        bool hadAuthority;
        internal void NotifyAuthority()
        {
            if (!hadAuthority && hasAuthority)
                OnStartAuthority();
            if (hadAuthority && !hasAuthority)
                OnStopAuthority();
            hadAuthority = hasAuthority;
        }

        internal void OnStartAuthority()
        {
            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                // an exception in OnStartAuthority should be caught, so that one
                // component's exception doesn't stop all other components from
                // being initialized
                // => this is what Unity does for Start() etc. too.
                //    one exception doesn't stop all the other Start() calls!
                try
                {
                    comp.OnStartAuthority();
                }
                catch (Exception e)
                {
                    Debug.LogException(e, comp);
                }
            }
        }

        internal void OnStopAuthority()
        {
            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                // an exception in OnStopAuthority should be caught, so that one
                // component's exception doesn't stop all other components from
                // being initialized
                // => this is what Unity does for Start() etc. too.
                //    one exception doesn't stop all the other Start() calls!
                try
                {
                    comp.OnStopAuthority();
                }
                catch (Exception e)
                {
                    Debug.LogException(e, comp);
                }
            }
        }

        // vis2k: readstring bug prevention: https://github.com/vis2k/Mirror/issues/2617
        // -> OnSerialize writes length,componentData,length,componentData,...
        // -> OnDeserialize carefully extracts each data, then deserializes each component with separate readers
        //    -> it will be impossible to read too many or too few bytes in OnDeserialize
        //    -> we can properly track down errors
        bool OnSerializeSafely(NetworkBehaviour comp, NetworkWriter writer, bool initialState)
        {
            // write placeholder length bytes
            // (jumping back later is WAY faster than allocating a temporary
            //  writer for the payload, then writing payload.size, payload)
            int headerPosition = writer.Position;
            // no varint because we don't know the final size yet
            writer.WriteInt(0);
            int contentPosition = writer.Position;

            // write payload
            bool result = false;
            try
            {
                result = comp.OnSerialize(writer, initialState);
            }
            catch (Exception e)
            {
                // show a detailed error and let the user know what went wrong
                Debug.LogError($"OnSerialize failed for: object={name} component={comp.GetType()} sceneId={sceneId:X}\n\n{e}");
            }
            int endPosition = writer.Position;

            // fill in length now
            writer.Position = headerPosition;
            writer.WriteInt(endPosition - contentPosition);
            writer.Position = endPosition;

            //Debug.Log($"OnSerializeSafely written for object {comp.name} component:{comp.GetType()} sceneId:{sceneId:X} header:{headerPosition} content:{contentPosition} end:{endPosition} contentSize:{endPosition - contentPosition}");

            return result;
        }

        // serialize all components using dirtyComponentsMask
        // check ownerWritten/observersWritten to know if anything was written
        // We pass dirtyComponentsMask into this function so that we can check
        // if any Components are dirty before creating writers
        internal void OnSerializeAllSafely(bool initialState, NetworkWriter ownerWriter, NetworkWriter observersWriter)
        {
            // check if components are in byte.MaxRange just to be 100% sure
            // that we avoid overflows
            NetworkBehaviour[] components = NetworkBehaviours;
            if (components.Length > byte.MaxValue)
                throw new IndexOutOfRangeException($"{name} has more than {byte.MaxValue} components. This is not supported.");

            // serialize all components
            for (int i = 0; i < components.Length; ++i)
            {
                // is this component dirty?
                // -> always serialize if initialState so all components are included in spawn packet
                // -> note: IsDirty() is false if the component isn't dirty or sendInterval isn't elapsed yet
                NetworkBehaviour comp = components[i];
                if (initialState || comp.IsDirty())
                {
                    //Debug.Log($"OnSerializeAllSafely: {name} -> {comp.GetType()} initial:{ initialState}");

                    // remember start position in case we need to copy it into
                    // observers writer too
                    int startPosition = ownerWriter.Position;

                    // write index as byte [0..255]
                    ownerWriter.WriteByte((byte)i);

                    // serialize into ownerWriter first
                    // (owner always gets everything!)
                    OnSerializeSafely(comp, ownerWriter, initialState);

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
                        ArraySegment<byte> segment = ownerWriter.ToArraySegment();
                        int length = ownerWriter.Position - startPosition;
                        observersWriter.WriteBytes(segment.Array, startPosition, length);
                    }
                }
            }
        }

        // get cached serialization for this tick (or serialize if none yet)
        // IMPORTANT: int tick avoids floating point inaccuracy over days/weeks
        internal NetworkIdentitySerialization GetSerializationAtTick(int tick)
        {
            // reserialize if tick is different than last changed.
            // NOTE: != instead of < because int.max+1 overflows at some point.
            if (lastSerialization.tick != tick)
            {
                // reset
                lastSerialization.ownerWriter.Position = 0;
                lastSerialization.observersWriter.Position = 0;

                // serialize
                OnSerializeAllSafely(false,
                                     lastSerialization.ownerWriter,
                                     lastSerialization.observersWriter);

                // set tick
                lastSerialization.tick = tick;
                //Debug.Log($"{name} (netId={netId}) serialized for tick={tickTimeStamp}");
            }

            // return it
            return lastSerialization;
        }

        void OnDeserializeSafely(NetworkBehaviour comp, NetworkReader reader, bool initialState)
        {
            // read header as 4 bytes and calculate this chunk's start+end
            int contentSize = reader.ReadInt();
            int chunkStart = reader.Position;
            int chunkEnd = reader.Position + contentSize;

            // call OnDeserialize and wrap it in a try-catch block so there's no
            // way to mess up another component's deserialization
            try
            {
                //Debug.Log($"OnDeserializeSafely: {comp.name} component:{comp.GetType()} sceneId:{sceneId:X} length:{contentSize}");
                comp.OnDeserialize(reader, initialState);
            }
            catch (Exception e)
            {
                // show a detailed error and let the user know what went wrong
                Debug.LogError($"OnDeserialize failed Exception={e.GetType()} (see below) object={name} component={comp.GetType()} sceneId={sceneId:X} length={contentSize}. Possible Reasons:\n" +
                               $"  * Do {comp.GetType()}'s OnSerialize and OnDeserialize calls write the same amount of data({contentSize} bytes)? \n" +
                               $"  * Was there an exception in {comp.GetType()}'s OnSerialize/OnDeserialize code?\n" +
                               $"  * Are the server and client the exact same project?\n" +
                               $"  * Maybe this OnDeserialize call was meant for another GameObject? The sceneIds can easily get out of sync if the Hierarchy was modified only in the client OR the server. Try rebuilding both.\n\n" +
                               $"Exception {e}");
            }

            // now the reader should be EXACTLY at 'before + size'.
            // otherwise the component read too much / too less data.
            if (reader.Position != chunkEnd)
            {
                // warn the user
                int bytesRead = reader.Position - chunkStart;
                Debug.LogWarning($"OnDeserialize was expected to read {contentSize} instead of {bytesRead} bytes for object:{name} component={comp.GetType()} sceneId={sceneId:X}. Make sure that OnSerialize and OnDeserialize write/read the same amount of data in all cases.");

                // fix the position, so the following components don't all fail
                reader.Position = chunkEnd;
            }
        }

        internal void OnDeserializeAllSafely(NetworkReader reader, bool initialState)
        {
            if (NetworkBehaviours == null)
            {
                Debug.LogError($"NetworkBehaviours array is null on {gameObject.name}!\n" +
                    $"Typically this can happen when a networked object is a child of a " +
                    $"non-networked parent that's disabled, preventing Awake on the networked object " +
                    $"from being invoked, where the NetworkBehaviours array is initialized.", gameObject);
                return;
            }

            // deserialize all components that were received
            NetworkBehaviour[] components = NetworkBehaviours;
            while (reader.Remaining > 0)
            {
                // read & check index [0..255]
                byte index = reader.ReadByte();
                if (index < components.Length)
                {
                    // deserialize this component
                    OnDeserializeSafely(components[index], reader, initialState);
                }
            }
        }

        // Helper function to handle Command/Rpc
        internal void HandleRemoteCall(int componentIndex, int functionHash, MirrorInvokeType invokeType, NetworkReader reader, NetworkConnectionToClient senderConnection = null)
        {
            // check if unity object has been destroyed
            if (this == null)
            {
                Debug.LogWarning($"{invokeType} [{functionHash}] received for deleted object [netId={netId}]");
                return;
            }

            // find the right component to invoke the function on
            if (componentIndex < 0 || componentIndex >= NetworkBehaviours.Length)
            {
                Debug.LogWarning($"Component [{componentIndex}] not found for [netId={netId}]");
                return;
            }

            NetworkBehaviour invokeComponent = NetworkBehaviours[componentIndex];
            if (!RemoteCallHelper.InvokeHandlerDelegate(functionHash, invokeType, reader, invokeComponent, senderConnection))
            {
                Debug.LogError($"Found no receiver for incoming {invokeType} [{functionHash}] on {gameObject.name}, the server and client should have the same NetworkBehaviour instances [netId={netId}].");
            }
        }

        // Runs on server
        internal CommandInfo GetCommandInfo(int componentIndex, int cmdHash)
        {
            // check if unity object has been destroyed
            if (this == null)
            {
                // error can be logged later
                return default;
            }

            // find the right component to invoke the function on
            if (0 <= componentIndex && componentIndex < NetworkBehaviours.Length)
            {
                NetworkBehaviour invokeComponent = NetworkBehaviours[componentIndex];
                return RemoteCallHelper.GetCommandInfo(cmdHash, invokeComponent);
            }
            else
            {
                // error can be logged later
                return default;
            }
        }

        internal void AddObserver(NetworkConnection conn)
        {
            if (observers == null)
            {
                Debug.LogError($"AddObserver for {gameObject} observer list is null");
                return;
            }

            if (observers.ContainsKey(conn.connectionId))
            {
                // if we try to add a connectionId that was already added, then
                // we may have generated one that was already in use.
                return;
            }

            // Debug.Log($"Added observer: {conn.address} added for {gameObject}");

            // if we previously had no observers, then clear all dirty bits once.
            // a monster's health may have changed while it had no observers.
            // but that change (= the dirty bits) don't matter as soon as the
            // first observer comes.
            // -> first observer gets full spawn packet
            // -> afterwards it gets delta packet
            //    => if we don't clear previous dirty bits, observer would get
            //       the health change because the bit was still set.
            //    => ultimately this happens because spawn doesn't reset dirty
            //       bits
            //    => which happens because spawn happens separately, instead of
            //       in Broadcast() (which will be changed in the future)
            //
            // NOTE that NetworkServer.Broadcast previously cleared dirty bits
            //      for ALL SPAWNED that don't have observers. that was super
            //      expensive. doing it when adding the first observer has the
            //      same result, without the O(N) iteration in Broadcast().
            //
            // TODO remove this after moving spawning into Broadcast()!
            if (observers.Count == 0)
            {
                ClearAllComponentsDirtyBits();
            }

            observers[conn.connectionId] = conn;
            conn.AddToObserving(this);
        }

        // this is used when a connection is destroyed, since the "observers" property is read-only
        internal void RemoveObserver(NetworkConnection conn)
        {
            observers?.Remove(conn.connectionId);
        }

        // Called when NetworkIdentity is destroyed
        internal void ClearObservers()
        {
            if (observers != null)
            {
                foreach (NetworkConnection conn in observers.Values)
                {
                    conn.RemoveFromObserving(this, true);
                }
                observers.Clear();
            }
        }

        /// <summary>Assign control of an object to a client via the client's NetworkConnection.</summary>
        // This causes hasAuthority to be set on the client that owns the object,
        // and NetworkBehaviour.OnStartAuthority will be called on that client.
        // This object then will be in the NetworkConnection.clientOwnedObjects
        // list for the connection.
        //
        // Authority can be removed with RemoveClientAuthority. Only one client
        // can own an object at any time. This does not need to be called for
        // player objects, as their authority is setup automatically.
        public bool AssignClientAuthority(NetworkConnection conn)
        {
            if (!isServer)
            {
                Debug.LogError("AssignClientAuthority can only be called on the server for spawned objects.");
                return false;
            }

            if (conn == null)
            {
                Debug.LogError($"AssignClientAuthority for {gameObject} owner cannot be null. Use RemoveClientAuthority() instead.");
                return false;
            }

            if (connectionToClient != null && conn != connectionToClient)
            {
                Debug.LogError($"AssignClientAuthority for {gameObject} already has an owner. Use RemoveClientAuthority() first.");
                return false;
            }

            SetClientOwner(conn);

            // The client will match to the existing object
            NetworkServer.SendChangeOwnerMessage(this, conn);

            clientAuthorityCallback?.Invoke(conn, this, true);

            return true;
        }

        /// <summary>Removes ownership for an object.</summary>
        // Applies to objects that had authority set by AssignClientAuthority,
        // or NetworkServer.Spawn with a NetworkConnection parameter included.
        // Authority cannot be removed for player objects.
        public void RemoveClientAuthority()
        {
            if (!isServer)
            {
                Debug.LogError("RemoveClientAuthority can only be called on the server for spawned objects.");
                return;
            }

            if (connectionToClient?.identity == this)
            {
                Debug.LogError("RemoveClientAuthority cannot remove authority for a player object");
                return;
            }

            if (connectionToClient != null)
            {
                clientAuthorityCallback?.Invoke(connectionToClient, this, false);
                NetworkConnectionToClient previousOwner = connectionToClient;
                connectionToClient = null;
                NetworkServer.SendChangeOwnerMessage(this, previousOwner);
            }
        }

        // Reset is called when the user hits the Reset button in the
        // Inspector's context menu or when adding the component the first time.
        // This function is only called in editor mode.
        //
        // Reset() seems to be called only for Scene objects.
        // we can't destroy them (they are always in the scene).
        // instead we disable them and call Reset().
        //
        // OLD COMMENT:
        // Marks the identity for future reset, this is because we cant reset
        // the identity during destroy as people might want to be able to read
        // the members inside OnDestroy(), and we have no way of invoking reset
        // after OnDestroy is called.
        internal void Reset()
        {
            // make sure to call this before networkBehavioursCache is cleared below
            ResetSyncObjects();

            hasSpawned = false;
            clientStarted = false;
            isClient = false;
            isServer = false;
            //isLocalPlayer = false; <- cleared AFTER ClearLocalPlayer below!

            // remove authority flag. This object may be unspawned, not destroyed, on client.
            hasAuthority = false;
            NotifyAuthority();

            netId = 0;
            connectionToServer = null;
            connectionToClient = null;

            ClearObservers();

            // clear local player if it was the local player,
            // THEN reset isLocalPlayer AFTERWARDS
            if (isLocalPlayer)
            {
                // only clear NetworkClient.localPlayer IF IT POINTS TO US!
                // see OnDestroy() comments. it does the same.
                // (https://github.com/vis2k/Mirror/issues/2635)
                if (NetworkClient.localPlayer == this)
                    NetworkClient.localPlayer = null;
            }

            previousLocalPlayer = null;
            isLocalPlayer = false;
        }

        // clear all component's dirty bits no matter what
        internal void ClearAllComponentsDirtyBits()
        {
            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                comp.ClearAllDirtyBits();
            }
        }

        // Clear only dirty component's dirty bits. ignores components which
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
            // ResetSyncObjects is called by Reset, which is called by Unity.
            // AddComponent() calls Reset().
            // AddComponent() is called before Awake().
            // so NetworkBehaviours may not be initialized yet.
            if (NetworkBehaviours == null)
                return;

            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                comp.ResetSyncObjects();
            }
        }
    }
}
