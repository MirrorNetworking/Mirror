using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Mirror.RemoteCalls;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;

#if UNITY_2021_2_OR_NEWER
using UnityEditor.SceneManagement;
#else
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
    [AddComponentMenu("Network/Network Identity")]
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

        /// <summary>isOwned is true on the client if this NetworkIdentity is one of the .owned entities of our connection on the server.</summary>
        // for example: main player & pets are owned. monsters & npcs aren't.
        public bool isOwned { get; internal set; }

        /// <summary>The set of network connections (players) that can see this object.</summary>
        public readonly Dictionary<int, NetworkConnectionToClient> observers =
            new Dictionary<int, NetworkConnectionToClient>();

        /// <summary>The unique network Id of this object (unique at runtime).</summary>
        public uint netId { get; internal set; }

        /// <summary>Unique identifier for NetworkIdentity objects within a scene, used for spawning scene objects.</summary>
        // persistent scene id <sceneHash/32,sceneId/32> (see AssignSceneID comments)
        [FormerlySerializedAs("m_SceneId"), HideInInspector]
        public ulong sceneId;

        // assetId used to spawn prefabs across the network.
        // originally a Guid, but a 4 byte uint is sufficient
        // (as suggested by james)
        //
        // it's also easier to work with for serialization etc.
        // serialized and visible in inspector for easier debugging
        [SerializeField, HideInInspector] uint _assetId;

        // The AssetId trick:
        //   Ideally we would have a serialized 'Guid m_AssetId' but Unity can't
        //   serialize it because Guid's internal bytes are private
        //
        //   Using just the Guid string would work, but it's 32 chars long and
        //   would then be sent over the network as 64 instead of 16 bytes
        //
        // => The solution is to serialize the string internally here and then
        //    use the real 'Guid' type for everything else via .assetId
        public uint assetId
        {
            get
            {
#if UNITY_EDITOR
                // old UNET comment:
                // This is important because sometimes OnValidate does not run
                // (like when adding NetworkIdentity to prefab with no child links)
                if (_assetId == 0)
                    SetupIDs();
#endif
                return _assetId;
            }
            // assetId is set internally when creating or duplicating a prefab
            internal set
            {
                // should never be empty
                if (value == 0)
                {
                    Debug.LogError($"Can not set AssetId to empty guid on NetworkIdentity '{name}', old assetId '{_assetId}'");
                    return;
                }

                // always set it otherwise.
                // for new prefabs,        it will set from 0 to N.
                // for duplicated prefabs, it will set from N to M.
                // either way, it's always set to a valid GUID.
                _assetId = value;
                // Debug.Log($"Setting AssetId on NetworkIdentity '{name}', new assetId '{value:X4}'");
            }
        }

        /// <summary>Make this object only exist when the game is running as a server (or host).</summary>
        [FormerlySerializedAs("m_ServerOnly")]
        [Tooltip("Prevents this object from being spawned / enabled on clients")]
        public bool serverOnly;

        // Set before Destroy is called so that OnDestroy doesn't try to destroy
        // the object again
        internal bool destroyCalled;

        /// <summary>Client's network connection to the server. This is only valid for player objects on the client.</summary>
        // TODO change to NetworkConnectionToServer, but might cause some breaking
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

        // get all NetworkBehaviour components
        public NetworkBehaviour[] NetworkBehaviours { get; private set; }

        // to save bandwidth, we send one 64 bit dirty mask
        // instead of 1 byte index per dirty component.
        // which means we can't allow > 64 components (it's enough).
        const int MaxNetworkBehaviours = 64;

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

        // Keep track of all sceneIds to detect scene duplicates
        static readonly Dictionary<ulong, NetworkIdentity> sceneIds =
            new Dictionary<ulong, NetworkIdentity>();

        // Helper function to handle Command/Rpc
        internal void HandleRemoteCall(byte componentIndex, ushort functionHash, RemoteCallType remoteCallType, NetworkReader reader, NetworkConnectionToClient senderConnection = null)
        {
            // check if unity object has been destroyed
            if (this == null)
            {
                Debug.LogWarning($"{remoteCallType} [{functionHash}] received for deleted object [netId={netId}]");
                return;
            }

            // find the right component to invoke the function on
            if (componentIndex >= NetworkBehaviours.Length)
            {
                Debug.LogWarning($"Component [{componentIndex}] not found for [netId={netId}]");
                return;
            }

            NetworkBehaviour invokeComponent = NetworkBehaviours[componentIndex];
            if (!RemoteProcedureCalls.Invoke(functionHash, remoteCallType, reader, invokeComponent, senderConnection))
            {
                Debug.LogError($"Found no receiver for incoming {remoteCallType} [{functionHash}] on {gameObject.name}, the server and client should have the same NetworkBehaviour instances [netId={netId}].");
            }
        }

        // RuntimeInitializeOnLoadMethod -> fast playmode without domain reload
        // internal so it can be called from NetworkServer & NetworkClient
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void ResetStatics()
        {
            // reset ALL statics
            ResetClientStatics();
            ResetServerStatics();
        }

        // reset only client sided statics.
        // don't touch server statics when calling StopClient in host mode.
        // https://github.com/vis2k/Mirror/issues/2954
        internal static void ResetClientStatics()
        {
            previousLocalPlayer = null;
            clientAuthorityCallback = null;
        }

        internal static void ResetServerStatics()
        {
            nextNetworkId = 1;
        }

        /// <summary>Gets the NetworkIdentity from the sceneIds dictionary with the corresponding id</summary>
        public static NetworkIdentity GetSceneIdentity(ulong id) => sceneIds[id];

        static uint nextNetworkId = 1;
        internal static uint GetNextNetworkId() => nextNetworkId++;

        /// <summary>Resets nextNetworkId = 1</summary>
        public static void ResetNextNetworkId() => nextNetworkId = 1;

        /// <summary>The delegate type for the clientAuthorityCallback.</summary>
        public delegate void ClientAuthorityCallback(NetworkConnectionToClient conn, NetworkIdentity identity, bool authorityState);

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
            // Get all NetworkBehaviour components, including children.
            // Some users need NetworkTransform on child bones, etc.
            // => Deterministic: https://forum.unity.com/threads/getcomponentsinchildren.4582/#post-33983
            // => Never null. GetComponents returns [] if none found.
            // => Include inactive. We need all child components.
            NetworkBehaviours = GetComponentsInChildren<NetworkBehaviour>(true);
            ValidateComponents();

            // initialize each one
            for (int i = 0; i < NetworkBehaviours.Length; ++i)
            {
                NetworkBehaviour component = NetworkBehaviours[i];
                component.netIdentity = this;
                component.ComponentIndex = (byte)i;
            }
        }

        void ValidateComponents()
        {
            if (NetworkBehaviours == null)
            {
                Debug.LogError($"NetworkBehaviours array is null on {gameObject.name}!\n" +
                    $"Typically this can happen when a networked object is a child of a " +
                    $"non-networked parent that's disabled, preventing Awake on the networked object " +
                    $"from being invoked, where the NetworkBehaviours array is initialized.", gameObject);
            }
            else if (NetworkBehaviours.Length > MaxNetworkBehaviours)
            {
                Debug.LogError($"NetworkIdentity {name} has too many NetworkBehaviour components: only {MaxNetworkBehaviours} NetworkBehaviour components are allowed in order to save bandwidth.", this);
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
            DisallowChildNetworkIdentities();
            SetupIDs();
#endif
        }

        // expose our AssetId Guid to uint mapping code in case projects need to map Guids to uint as well.
        // this way their projects won't break if we change our mapping algorithm.
        // needs to be available at runtime / builds, don't wrap in #if UNITY_EDITOR
        public static uint AssetGuidToUint(Guid guid) => (uint)guid.GetHashCode(); // deterministic

#if UNITY_EDITOR
        // child NetworkIdentities are not supported.
        // Disallow them and show an error for the user to fix.
        // This needs to work for Prefabs & Scene objects, so the previous check
        // in NetworkClient.RegisterPrefab is not enough.
        void DisallowChildNetworkIdentities()
        {
#if UNITY_2020_3_OR_NEWER
            NetworkIdentity[] identities = GetComponentsInChildren<NetworkIdentity>(true);
#else
            NetworkIdentity[] identities = GetComponentsInChildren<NetworkIdentity>();
#endif
            if (identities.Length > 1)
            {
                // always log the next child component so it's easy to fix.
                // if there are multiple, then after removing it'll log the next.
                Debug.LogError($"'{name}' has another NetworkIdentity component on '{identities[1].name}'. There should only be one NetworkIdentity, and it must be on the root object. Please remove the other one.");
            }
        }

        void AssignAssetID(string path)
        {
            // only set if not empty. fixes https://github.com/vis2k/Mirror/issues/2765
            if (!string.IsNullOrWhiteSpace(path))
            {
                // if we generate the assetId then we MUST be sure to set dirty
                // in order to save the prefab object properly. otherwise it
                // would be regenerated every time we reopen the prefab.
                // -> Undo.RecordObject is the new EditorUtility.SetDirty!
                // -> we need to call it before changing.
                //
                // to verify this, duplicate a prefab and double click to open it.
                // add a log message if "_assetId != before_".
                // without RecordObject, it'll log every time because it's not saved.
                Undo.RecordObject(this, "Assigned AssetId");

                // uint before = _assetId;
                Guid guid = new Guid(AssetDatabase.AssetPathToGUID(path));
                assetId = AssetGuidToUint(guid);
                // if (_assetId != before) Debug.Log($"Assigned assetId={assetId} to {name}");
            }
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
                    _assetId = 0;
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

            if (isClient)
            {
                // ServerChangeScene doesn't send destroy messages.
                // some identities may persist in DDOL.
                // some are destroyed by scene change.
                // if an identity is still in .owned remove it.
                // fixes: https://github.com/MirrorNetworking/Mirror/issues/3308
                if (NetworkClient.connection != null)
                    NetworkClient.connection.owned.Remove(this);

                // if an identity is still in .spawned, remove it too.
                // fixes: https://github.com/MirrorNetworking/Mirror/issues/3324
                NetworkClient.spawned.Remove(netId);
            }
        }

        internal void OnStartServer()
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
            if (clientStarted) return;

            clientStarted = true;

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
            // In case this object was destroyed already don't call
            // OnStopClient if OnStartClient hasn't been called.
            if (!clientStarted) return;

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

        internal static NetworkIdentity previousLocalPlayer = null;
        internal void OnStartLocalPlayer()
        {
            // ensure OnStartLocalPlayer is only called once.
            // Room demo would call it multiple times:
            // - once from ApplySpawnPayload
            // - once from OnObjectSpawnFinished
            //
            // to reproduce:
            // - open room demo, add the 3 scenes to build settings
            // - add OnStartLocalPlayer log to RoomPlayer prefab
            // - build, run server-only
            // - in editor, connect, press ready
            // - in server, start game
            // - notice multiple OnStartLocalPlayer logs in editor client
            //
            // explanation:
            // we send the spawn message multiple times. Whenever an object changes
            // authority, we send the spawn message again for the object. This is
            // necessary because we need to reinitialize all variables when
            // ownership change due to sync to owner feature.
            // Without this static, the second time we get the spawn message we
            // would call OnStartLocalPlayer again on the same object
            if (previousLocalPlayer == this)
                return;
            previousLocalPlayer = this;

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

        internal void OnStopLocalPlayer()
        {
            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                // an exception in OnStopLocalPlayer should be caught, so that
                // one component's exception doesn't stop all other components
                // from being initialized
                // => this is what Unity does for Start() etc. too.
                //    one exception doesn't stop all the other Start() calls!
                try
                {
                    comp.OnStopLocalPlayer();
                }
                catch (Exception e)
                {
                    Debug.LogException(e, comp);
                }
            }
        }

        // build dirty mask for server owner & observers (= all dirty components).
        // faster to do it in one iteration instead of iterating separately.
        (ulong, ulong) ServerDirtyMasks(bool initialState)
        {
            ulong ownerMask = 0;
            ulong observerMask = 0;

            NetworkBehaviour[] components = NetworkBehaviours;
            for (int i = 0; i < components.Length; ++i)
            {
                NetworkBehaviour component = components[i];

                bool dirty = component.IsDirty();
                ulong nthBit = (1u << i);

                // owner needs to be considered for both SyncModes, because
                // Observers mode always includes the Owner.
                //
                // for initial, it should always sync owner.
                // for delta, only for ServerToClient and only if dirty.
                //     ClientToServer comes from the owner client.
                if (initialState || (component.syncDirection == SyncDirection.ServerToClient && dirty))
                    ownerMask |= nthBit;

                // observers need to be considered only in Observers mode
                //
                // for initial, it should always sync to observers.
                // for delta, only if dirty.
                // SyncDirection is irrelevant, as both are broadcast to
                // observers which aren't the owner.
                if (component.syncMode == SyncMode.Observers && (initialState || dirty))
                    observerMask |= nthBit;
            }

            return (ownerMask, observerMask);
        }

        // build dirty mask for client.
        // server always knows initialState, so we don't need it here.
        ulong ClientDirtyMask()
        {
            ulong mask = 0;

            NetworkBehaviour[] components = NetworkBehaviours;
            for (int i = 0; i < components.Length; ++i)
            {
                // on the client, we need to consider different sync scenarios:
                //
                //   ServerToClient SyncDirection:
                //     do nothing.
                //   ClientToServer SyncDirection:
                //     serialize only if owned.

                // on client, only consider owned components with SyncDirection to server
                NetworkBehaviour component = components[i];
                if (isOwned && component.syncDirection == SyncDirection.ClientToServer)
                {
                    // set the n-th bit if dirty
                    // shifting from small to large numbers is varint-efficient.
                    if (component.IsDirty()) mask |= (1u << i);
                }
            }

            return mask;
        }

        // check if n-th component is dirty.
        // in other words, if it has the n-th bit set in the dirty mask.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsDirty(ulong mask, int index)
        {
            ulong nthBit = (ulong)(1 << index);
            return (mask & nthBit) != 0;
        }

        // serialize components into writer on the server.
        // check ownerWritten/observersWritten to know if anything was written
        // We pass dirtyComponentsMask into this function so that we can check
        // if any Components are dirty before creating writers
        internal void SerializeServer(bool initialState, NetworkWriter ownerWriter, NetworkWriter observersWriter)
        {
            // ensure NetworkBehaviours are valid before usage
            ValidateComponents();
            NetworkBehaviour[] components = NetworkBehaviours;

            // check which components are dirty for owner / observers.
            // this is quite complicated with SyncMode + SyncDirection.
            // see the function for explanation.
            //
            // instead of writing a 1 byte index per component,
            // we limit components to 64 bits and write one ulong instead.
            // the ulong is also varint compressed for minimum bandwidth.
            (ulong ownerMask, ulong observerMask) = ServerDirtyMasks(initialState);

            // if nothing dirty, then don't even write the mask.
            // otherwise, every unchanged object would send a 1 byte dirty mask!
            if (ownerMask != 0) Compression.CompressVarUInt(ownerWriter, ownerMask);
            if (observerMask != 0) Compression.CompressVarUInt(observersWriter, observerMask);

            // serialize all components
            // perf: only iterate if either dirty mask has dirty bits.
            if ((ownerMask | observerMask) != 0)
            {
                for (int i = 0; i < components.Length; ++i)
                {
                    NetworkBehaviour comp = components[i];

                    // is the component dirty for anyone (owner or observers)?
                    // may be serialized to owner, observer, both, or neither.
                    //
                    // OnSerialize should only be called once.
                    // this is faster, and it cleaner because it may set
                    // internal state, counters, logs, etc.
                    //
                    // previously we always serialized to owner and then copied
                    // the serialization to observers. however, since
                    // SyncDirection it's not guaranteed to be in owner anymore.
                    // so we need to serialize to temporary writer first.
                    // and then copy as needed.
                    bool ownerDirty = IsDirty(ownerMask, i);
                    bool observersDirty = IsDirty(observerMask, i);
                    if (ownerDirty || observersDirty)
                    {
                        // serialize into helper writer
                        using (NetworkWriterPooled temp = NetworkWriterPool.Get())
                        {
                            comp.Serialize(temp, initialState);
                            ArraySegment<byte> segment = temp.ToArraySegment();

                            // copy to owner / observers as needed
                            if (ownerDirty) ownerWriter.WriteBytes(segment.Array, segment.Offset, segment.Count);
                            if (observersDirty) observersWriter.WriteBytes(segment.Array, segment.Offset, segment.Count);
                        }

                        // clear dirty bits for the components that we serialized.
                        // do not clear for _all_ components, only the ones that
                        // were dirty and had their syncInterval elapsed.
                        //
                        // we don't want to clear bits before the syncInterval
                        // was elapsed, as then they wouldn't be synced.
                        //
                        // only clear for delta, not for full (spawn messages).
                        // otherwise if a player joins, we serialize monster,
                        // and shouldn't clear dirty bits not yet synced to
                        // other players.
                        if (!initialState) comp.ClearAllDirtyBits();
                    }
                }
            }
        }

        // serialize components into writer on the client.
        internal void SerializeClient(NetworkWriter writer)
        {
            // ensure NetworkBehaviours are valid before usage
            ValidateComponents();
            NetworkBehaviour[] components = NetworkBehaviours;

            // check which components are dirty.
            // this is quite complicated with SyncMode + SyncDirection.
            // see the function for explanation.
            //
            // instead of writing a 1 byte index per component,
            // we limit components to 64 bits and write one ulong instead.
            // the ulong is also varint compressed for minimum bandwidth.
            ulong dirtyMask = ClientDirtyMask();

            // varint compresses the mask to 1 byte in most cases.
            // instead of writing an 8 byte ulong.
            //   7 components fit into 1 byte.  (previously  7 bytes)
            //  11 components fit into 2 bytes. (previously 11 bytes)
            //  16 components fit into 3 bytes. (previously 16 bytes)
            // TODO imer: server knows amount of comps, write N bytes instead

            // if nothing dirty, then don't even write the mask.
            // otherwise, every unchanged object would send a 1 byte dirty mask!
            if (dirtyMask != 0) Compression.CompressVarUInt(writer, dirtyMask);

            // serialize all components
            // perf: only iterate if dirty mask has dirty bits.
            if (dirtyMask != 0)
            {
                // serialize all components
                for (int i = 0; i < components.Length; ++i)
                {
                    NetworkBehaviour comp = components[i];

                    // is this component dirty?
                    // reuse the mask instead of calling comp.IsDirty() again here.
                    if (IsDirty(dirtyMask, i))
                    // if (isOwned && component.syncDirection == SyncDirection.ClientToServer)
                    {
                        // serialize into writer.
                        // server always knows initialState, we never need to send it
                        comp.Serialize(writer, false);

                        // clear dirty bits for the components that we serialized.
                        // do not clear for _all_ components, only the ones that
                        // were dirty and had their syncInterval elapsed.
                        //
                        // we don't want to clear bits before the syncInterval
                        // was elapsed, as then they wouldn't be synced.
                        comp.ClearAllDirtyBits();
                    }
                }
            }
        }

        // deserialize components from the client on the server.
        // there's no 'initialState'. server always knows the initial state.
        internal bool DeserializeServer(NetworkReader reader)
        {
            // ensure NetworkBehaviours are valid before usage
            ValidateComponents();
            NetworkBehaviour[] components = NetworkBehaviours;

            // first we deserialize the varinted dirty mask
            ulong mask = Compression.DecompressVarUInt(reader);

            // now deserialize every dirty component
            for (int i = 0; i < components.Length; ++i)
            {
                // was this one dirty?
                if (IsDirty(mask, i))
                {
                    NetworkBehaviour comp = components[i];

                    // safety check to ensure clients can only modify their own
                    // ClientToServer components, nothing else.
                    if (comp.syncDirection == SyncDirection.ClientToServer)
                    {
                        // deserialize this component
                        // server always knows the initial state (initial=false)
                        // disconnect if failed, to prevent exploits etc.
                        if (!comp.Deserialize(reader, false)) return false;

                        // server received state from the owner client.
                        // set dirty so it's broadcast to other clients too.
                        //
                        // note that we set the _whole_ component as dirty.
                        // everything will be broadcast to others.
                        // SetSyncVarDirtyBits() would be nicer, but not all
                        // components use [SyncVar]s.
                        comp.SetDirty();
                    }
                }
            }

            // successfully deserialized everything
            return true;
        }

        // deserialize components from server on the client.
        internal void DeserializeClient(NetworkReader reader, bool initialState)
        {
            // ensure NetworkBehaviours are valid before usage
            ValidateComponents();
            NetworkBehaviour[] components = NetworkBehaviours;

            // first we deserialize the varinted dirty mask
            ulong mask = Compression.DecompressVarUInt(reader);

            // now deserialize every dirty component
            for (int i = 0; i < components.Length; ++i)
            {
                // was this one dirty?
                if (IsDirty(mask, i))
                {
                    // deserialize this component
                    NetworkBehaviour comp = components[i];
                    comp.Deserialize(reader, initialState);
                }
            }
        }

        // get cached serialization for this tick (or serialize if none yet).
        // IMPORTANT: int tick avoids floating point inaccuracy over days/weeks.
        // calls SerializeServer, so this function is to be called on server.
        internal NetworkIdentitySerialization GetServerSerializationAtTick(int tick)
        {
            // only rebuild serialization once per tick. reuse otherwise.
            // except for tests, where Time.frameCount never increases.
            // so during tests, we always rebuild.
            // (otherwise [SyncVar] changes would never be serialized in tests)
            //
            // NOTE: != instead of < because int.max+1 overflows at some point.
            if (lastSerialization.tick != tick
#if UNITY_EDITOR
                || !Application.isPlaying
#endif
               )
            {
                // reset
                lastSerialization.ownerWriter.Position = 0;
                lastSerialization.observersWriter.Position = 0;

                // serialize
                SerializeServer(false,
                                lastSerialization.ownerWriter,
                                lastSerialization.observersWriter);

                // set tick
                lastSerialization.tick = tick;
                //Debug.Log($"{name} (netId={netId}) serialized for tick={tickTimeStamp}");
            }

            // return it
            return lastSerialization;
        }

        internal void AddObserver(NetworkConnectionToClient conn)
        {
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

        // clear all component's dirty bits no matter what
        internal void ClearAllComponentsDirtyBits()
        {
            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                comp.ClearAllDirtyBits();
            }
        }

        // this is used when a connection is destroyed, since the "observers" property is read-only
        internal void RemoveObserver(NetworkConnection conn)
        {
            observers.Remove(conn.connectionId);
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
        public bool AssignClientAuthority(NetworkConnectionToClient conn)
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

        // used when adding players
        internal void SetClientOwner(NetworkConnectionToClient conn)
        {
            // do nothing if it already has an owner
            if (connectionToClient != null && conn != connectionToClient)
            {
                Debug.LogError($"Object {this} netId={netId} already has an owner. Use RemoveClientAuthority() first", this);
                return;
            }

            // otherwise set the owner connection
            connectionToClient = conn;
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
        // Do not reset SyncObjects from Reset
        // - Unspawned objects need to retain their list contents
        // - They may be respawned, especially players, but others as well.
        //
        // OLD COMMENT:
        // Marks the identity for future reset, this is because we cant reset
        // the identity during destroy as people might want to be able to read
        // the members inside OnDestroy(), and we have no way of invoking reset
        // after OnDestroy is called.
        internal void Reset()
        {
            hasSpawned = false;
            clientStarted = false;
            isClient = false;
            isServer = false;
            //isLocalPlayer = false; <- cleared AFTER ClearLocalPlayer below!

            // remove authority flag. This object may be unspawned, not destroyed, on client.
            isOwned = false;
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

        bool hadAuthority;
        internal void NotifyAuthority()
        {
            if (!hadAuthority && isOwned)
                OnStartAuthority();
            if (hadAuthority && !isOwned)
                OnStopAuthority();
            hadAuthority = isOwned;
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

        // Called when NetworkIdentity is destroyed
        internal void ClearObservers()
        {
            foreach (NetworkConnectionToClient conn in observers.Values)
            {
                conn.RemoveFromObserving(this, true);
            }
            observers.Clear();
        }
    }
}
