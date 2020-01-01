using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
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
        // configuration
        NetworkBehaviour[] networkBehavioursCache;

        // member used to mark a identity for future reset
        // check MarkForReset for more information.
        bool reset;

        /// <summary>
        /// Returns true if running as a client and this object was spawned by a server.
        /// </summary>
        public bool isClient => NetworkClient.active && netId != 0 && !serverOnly;

        /// <summary>
        /// Returns true if NetworkServer.active and server is not stopped.
        /// </summary>
        public bool isServer => NetworkServer.active && netId != 0;

        /// <summary>
        /// This returns true if this object is the one that represents the player on the local machine.
        /// <para>This is set when the server has spawned an object for this particular client.</para>
        /// </summary>
        public bool isLocalPlayer => ClientScene.localPlayer == this;

        /// <summary>
        /// This returns true if this object is the authoritative player object on the client.
        /// <para>This value is determined at runtime. For most objects, authority is held by the server.</para>
        /// <para>For objects that had their authority set by AssignClientAuthority on the server, this will be true on the client that owns the object. NOT on other clients.</para>
        /// </summary>
        public bool hasAuthority { get; internal set; }

        /// <summary>
        /// The set of network connections (players) that can see this object.
        /// <para>null until OnStartServer was called. this is necessary for SendTo* to work properly in server-only mode.</para>
        /// </summary>
        public Dictionary<int, NetworkConnection> observers;

        /// <summary>
        /// Unique identifier for this particular object instance, used for tracking objects between networked clients and the server.
        /// <para>This is a unique identifier for this particular GameObject instance. Use it to track GameObjects between networked clients and the server.</para>
        /// </summary>
        public uint netId { get; internal set; }

        /// <summary>
        /// A unique identifier for NetworkIdentity objects within a scene.
        /// <para>This is used for spawning scene objects on clients.</para>
        /// </summary>
        public ulong sceneId => m_SceneId;

        /// <summary>
        /// Flag to make this object only exist when the game is running as a server (or host).
        /// </summary>
        [FormerlySerializedAs("m_ServerOnly")]
        public bool serverOnly;

        /// <summary>
        /// Obsolete: Use <see cref="connectionToClient" /> instead
        /// </summary>
        [Obsolete("Use connectionToClient instead")]
        public NetworkConnectionToClient clientAuthorityOwner => connectionToClient;

        /// <summary>
        /// The NetworkConnection associated with this NetworkIdentity. This is only valid for player objects on a local client.
        /// </summary>
        public NetworkConnection connectionToServer { get; internal set; }


        private NetworkConnectionToClient _connectionToClient;
        /// <summary>
        /// The NetworkConnection associated with this <see cref="NetworkIdentity">NetworkIdentity.</see> This is valid for player and other owned objects in the server.
        /// <para>Use it to return details such as the connection&apos;s identity, IP address and ready status.</para>
        /// </summary>
        public NetworkConnectionToClient connectionToClient
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

        /// <summary>
        /// All spawned NetworkIdentities by netId. Available on server and client.
        /// </summary>
        public static readonly Dictionary<uint, NetworkIdentity> spawned = new Dictionary<uint, NetworkIdentity>();

        public NetworkBehaviour[] NetworkBehaviours => networkBehavioursCache = networkBehavioursCache ?? GetComponents<NetworkBehaviour>();

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
        public Guid assetId
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
                else Debug.LogWarning($"SetDynamicAssetId object {this.name} already has an assetId {m_AssetId}, new asset id {newAssetIdString}");
            }
        }

        // persistent scene id <sceneHash/32,sceneId/32>
        // (see AssignSceneID comments)
        //  suppress "Field 'NetworkIdentity.m_SceneId' is never assigned to, and will always have its default value 0"
        // when building standalone
#pragma warning disable CS0649
        [SerializeField, HideInInspector] ulong m_SceneId;
#pragma warning restore CS0649

        // keep track of all sceneIds to detect scene duplicates
        static readonly Dictionary<ulong, NetworkIdentity> sceneIds = new Dictionary<ulong, NetworkIdentity>();

        public static NetworkIdentity GetSceneIdenity(ulong id) => sceneIds[id];

        // used when adding players
        internal void SetClientOwner(NetworkConnection conn)
        {
            if (connectionToClient != null && conn != connectionToClient)
            {
                Debug.LogError($"Object {this} netId={netId} already has an owner", this);
            }
            connectionToClient = (NetworkConnectionToClient)conn;
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
        public delegate void ClientAuthorityCallback(NetworkConnection conn, NetworkIdentity identity, bool authorityState);

        /// <summary>
        /// A callback that can be populated to be notified when the client-authority state of objects changes.
        /// <para>Whenever an object is spawned using SpawnWithClientAuthority, or the client authority status of an object is changed with AssignClientAuthority or RemoveClientAuthority, then this callback will be invoked.</para>
        /// <para>This callback is only invoked on the server.</para>
        /// </summary>
        public static ClientAuthorityCallback clientAuthorityCallback;

        // this is used when a connection is destroyed, since the "observers" property is read-only
        internal void RemoveObserverInternal(NetworkConnection conn)
        {
            observers?.Remove(conn.connectionId);
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
                    Debug.LogError(name + "'s sceneId: " + sceneId.ToString("X") + " is already taken by: " + existing.name + ". Don't call Instantiate for NetworkIdentities that were in the scene since the beginning (aka scene objects). Otherwise the client won't know which object to use for a SpawnSceneObject message.");
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
                Debug.LogError("Failed to find prefab parent for scene object [name:" + gameObject.name + "]");
                return false;
            }
            return true;
        }

        static uint GetRandomUInt()
        {
            // use Crypto RNG to avoid having time based duplicates
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
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
            bool duplicate = sceneIds.TryGetValue(m_SceneId, out NetworkIdentity existing) && existing != null && existing != this;
            if (m_SceneId == 0 || duplicate)
            {
                // clear in any case, because it might have been a duplicate
                m_SceneId = 0;

                // if a scene was never opened and we are building it, then a
                // sceneId would be assigned to build but not saved in editor,
                // resulting in them getting out of sync.
                // => don't ever assign temporary ids. they always need to be
                //    permanent
                // => throw an exception to cancel the build and let the user
                //    know how to fix it!
                if (BuildPipeline.isBuildingPlayer)
                    throw new Exception("Scene " + gameObject.scene.path + " needs to be opened and resaved before building, because the scene object " + name + " has no valid sceneId yet.");

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
                    m_SceneId = randomId;
                    //Debug.Log(name + " in scene=" + gameObject.scene.name + " sceneId assigned to: " + m_SceneId.ToString("X"));
                }
            }

            // add to sceneIds dict no matter what
            // -> even if we didn't generate anything new, because we still need
            //    existing sceneIds in there to check duplicates
            sceneIds[m_SceneId] = this;
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
            m_SceneId = (m_SceneId & 0xFFFFFFFF) | shiftedHash;

            // log it. this is incredibly useful to debug sceneId issues.
            if (LogFilter.Debug) Debug.Log(name + " in scene=" + gameObject.scene.name + " scene index hash(" + pathHash.ToString("X") + ") copied into sceneId: " + m_SceneId.ToString("X"));
        }

        void SetupIDs()
        {
            if (ThisIsAPrefab())
            {
                m_SceneId = 0; // force 0 for prefabs
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
                    m_SceneId = 0; // force 0 for prefabs
                    //Debug.Log(name + " @ scene: " + gameObject.scene.name + " sceneid reset to 0 because CurrentPrefabStage=" + PrefabStageUtility.GetCurrentPrefabStage() + " PrefabStage=" + PrefabStageUtility.GetPrefabStage(gameObject));
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

        void OnDestroy()
        {
            // remove from sceneIds
            // -> remove with (0xFFFFFFFFFFFFFFFF) and without (0x00000000FFFFFFFF)
            //    sceneHash to be 100% safe.
            sceneIds.Remove(sceneId);
            sceneIds.Remove(sceneId & 0x00000000FFFFFFFF);

            if (isServer)
            {
                NetworkServer.Destroy(gameObject);
            }
        }

        internal void OnStartServer()
        {
            // If the instance/net ID is invalid here then this is an object instantiated from a prefab and the server should assign a valid ID
            if (netId != 0)
            {
                // This object has already been spawned, this method might be called again
                // if we try to respawn all objects.  This can happen when we add a scene
                // in that case there is nothing else to do.
                return;
            }

            netId = GetNextNetworkId();
            observers = new Dictionary<int, NetworkConnection>();

            if (LogFilter.Debug) Debug.Log("OnStartServer " + this + " NetId:" + netId + " SceneId:" + sceneId);

            // add to spawned (note: the original EnableIsServer isn't needed
            // because we already set m_isServer=true above)
            spawned[netId] = this;

            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                try
                {
                    comp.OnStartServer();
                }
                catch (Exception e)
                {
                    Debug.LogError("Exception in OnStartServer:" + e.Message + " " + e.StackTrace);
                }
            }
        }

        bool clientStarted;
        internal void OnStartClient()
        {
            if (clientStarted)
                return;
            clientStarted = true;

            if (LogFilter.Debug) Debug.Log("OnStartClient " + gameObject + " netId:" + netId);
            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                try
                {
                    comp.OnStartClient(); // user implemented startup
                }
                catch (Exception e)
                {
                    Debug.LogError("Exception in OnStartClient:" + e.Message + " " + e.StackTrace);
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

        void OnStartAuthority()
        {
            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                try
                {
                    comp.OnStartAuthority();
                }
                catch (Exception e)
                {
                    Debug.LogError("Exception in OnStartAuthority:" + e.Message + " " + e.StackTrace);
                }
            }
        }

        void OnStopAuthority()
        {
            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                try
                {
                    comp.OnStopAuthority();
                }
                catch (Exception e)
                {
                    Debug.LogError("Exception in OnStopAuthority:" + e.Message + " " + e.StackTrace);
                }
            }
        }

        internal void OnSetHostVisibility(bool visible)
        {
            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                try
                {
#pragma warning disable 618
                    comp.OnSetLocalVisibility(visible); // remove later!
#pragma warning restore 618
                    comp.OnSetHostVisibility(visible);
                }
                catch (Exception e)
                {
                    Debug.LogError("Exception in OnSetLocalVisibility:" + e.Message + " " + e.StackTrace);
                }
            }
        }

        internal bool OnCheckObserver(NetworkConnection conn)
        {
            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                try
                {
                    if (!comp.OnCheckObserver(conn))
                        return false;
                }
                catch (Exception e)
                {
                    Debug.LogError("Exception in OnCheckObserver:" + e.Message + " " + e.StackTrace);
                }
            }
            return true;
        }

        // vis2k: readstring bug prevention: https://issuetracker.unity3d.com/issues/unet-networkwriter-dot-write-causing-readstring-slash-readbytes-out-of-range-errors-in-clients
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
            writer.WriteInt32(0);
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
                Debug.LogError("OnSerialize failed for: object=" + name + " component=" + comp.GetType() + " sceneId=" + m_SceneId.ToString("X") + "\n\n" + e);
            }
            int endPosition = writer.Position;

            // fill in length now
            writer.Position = headerPosition;
            writer.WriteInt32(endPosition - contentPosition);
            writer.Position = endPosition;

            if (LogFilter.Debug) Debug.Log("OnSerializeSafely written for object=" + comp.name + " component=" + comp.GetType() + " sceneId=" + m_SceneId.ToString("X") + "header@" + headerPosition + " content@" + contentPosition + " end@" + endPosition + " contentSize=" + (endPosition - contentPosition));

            return result;
        }

        // serialize all components (or only dirty ones if not initial state)
        // -> check ownerWritten/observersWritten to know if anything was written
        internal void OnSerializeAllSafely(bool initialState, NetworkWriter ownerWriter, out int ownerWritten, NetworkWriter observersWriter, out int observersWritten)
        {
            // clear 'written' variables
            ownerWritten = observersWritten = 0;

            if (NetworkBehaviours.Length > 64)
            {
                Debug.LogError("Only 64 NetworkBehaviour components are allowed for NetworkIdentity: " + name + " because of the dirtyComponentMask");
                return;
            }
            ulong dirtyComponentsMask = GetDirtyMask(initialState);

            if (dirtyComponentsMask == 0L)
                return;

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
                    if (LogFilter.Debug) Debug.Log("OnSerializeAllSafely: " + name + " -> " + comp.GetType() + " initial=" + initialState);

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
                        ArraySegment<byte> segment = ownerWriter.ToArraySegment();
                        int length = ownerWriter.Position - startPosition;
                        observersWriter.WriteBytes(segment.Array, startPosition, length);
                        ++observersWritten;
                    }
                }
            }
        }

        internal ulong GetDirtyMask(bool initialState)
        {
            // loop through all components only once and then write dirty+payload into the writer afterwards
            ulong dirtyComponentsMask = 0L;
            NetworkBehaviour[] components = NetworkBehaviours;
            for (int i = 0; i < components.Length; ++i)
            {
                NetworkBehaviour comp = components[i];
                if (initialState || comp.IsDirty())
                {
                    dirtyComponentsMask |= (ulong)(1L << i);
                }
            }

            return dirtyComponentsMask;
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
            // read header as 4 bytes and calculate this chunk's start+end
            int contentSize = reader.ReadInt32();
            int chunkStart = reader.Position;
            int chunkEnd = reader.Position + contentSize;

            // call OnDeserialize and wrap it in a try-catch block so there's no
            // way to mess up another component's deserialization
            try
            {
                if (LogFilter.Debug) Debug.Log("OnDeserializeSafely: " + comp.name + " component=" + comp.GetType() + " sceneId=" + m_SceneId.ToString("X") + " length=" + contentSize);
                comp.OnDeserialize(reader, initialState);
            }
            catch (Exception e)
            {
                // show a detailed error and let the user know what went wrong
                Debug.LogError("OnDeserialize failed for: object=" + name + " component=" + comp.GetType() + " sceneId=" + m_SceneId.ToString("X") + " length=" + contentSize + ". Possible Reasons:\n  * Do " + comp.GetType() + "'s OnSerialize and OnDeserialize calls write the same amount of data(" + contentSize + " bytes)? \n  * Was there an exception in " + comp.GetType() + "'s OnSerialize/OnDeserialize code?\n  * Are the server and client the exact same project?\n  * Maybe this OnDeserialize call was meant for another GameObject? The sceneIds can easily get out of sync if the Hierarchy was modified only in the client OR the server. Try rebuilding both.\n\n" + e);
            }

            // now the reader should be EXACTLY at 'before + size'.
            // otherwise the component read too much / too less data.
            if (reader.Position != chunkEnd)
            {
                // warn the user
                int bytesRead = reader.Position - chunkStart;
                Debug.LogWarning("OnDeserialize was expected to read " + contentSize + " instead of " + bytesRead + " bytes for object:" + name + " component=" + comp.GetType() + " sceneId=" + m_SceneId.ToString("X") + ". Make sure that OnSerialize and OnDeserialize write/read the same amount of data in all cases.");

                // fix the position, so the following components don't all fail
                reader.Position = chunkEnd;
            }
        }

        internal void OnDeserializeAllSafely(NetworkReader reader, bool initialState)
        {
            // read component dirty mask
            ulong dirtyComponentsMask = reader.ReadPackedUInt64();

            NetworkBehaviour[] components = NetworkBehaviours;
            // loop through all components and deserialize the dirty ones
            for (int i = 0; i < components.Length; ++i)
            {
                // is the dirty bit at position 'i' set to 1?
                ulong dirtyBit = (ulong)(1L << i);
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
                Debug.LogWarning(invokeType + " [" + functionHash + "] received for deleted object [netId=" + netId + "]");
                return;
            }

            // find the right component to invoke the function on
            if (0 <= componentIndex && componentIndex < NetworkBehaviours.Length)
            {
                NetworkBehaviour invokeComponent = NetworkBehaviours[componentIndex];
                if (!invokeComponent.InvokeHandlerDelegate(functionHash, invokeType, reader))
                {
                    Debug.LogError("Found no receiver for incoming " + invokeType + " [" + functionHash + "] on " + gameObject + ",  the server and client should have the same NetworkBehaviour instances [netId=" + netId + "].");
                }
            }
            else
            {
                Debug.LogWarning("Component [" + componentIndex + "] not found for [netId=" + netId + "]");
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
        internal void HandleRPC(int componentIndex, int rpcHash, NetworkReader reader)
        {
            HandleRemoteCall(componentIndex, rpcHash, MirrorInvokeType.ClientRpc, reader);
        }

        internal void OnUpdateVars(NetworkReader reader, bool initialState)
        {
            OnDeserializeAllSafely(reader, initialState);
        }

        private static NetworkIdentity previousLocalPlayer = null;
        internal void OnStartLocalPlayer()
        {
            if (previousLocalPlayer == this)
                return;
            previousLocalPlayer = this;

            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                comp.OnStartLocalPlayer();
            }
        }

        internal void OnNetworkDestroy()
        {
            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                comp.OnNetworkDestroy();
            }
        }

        internal void ClearObservers()
        {
            if (observers != null)
            {
                foreach (NetworkConnection conn in observers.Values)
                {
                    conn.RemoveFromVisList(this, true);
                }
                observers.Clear();
            }
        }

        internal void AddObserver(NetworkConnection conn)
        {
            if (observers == null)
            {
                Debug.LogError("AddObserver for " + gameObject + " observer list is null");
                return;
            }

            if (observers.ContainsKey(conn.connectionId))
            {
                // if we try to add a connectionId that was already added, then
                // we may have generated one that was already in use.
                return;
            }

            if (LogFilter.Debug) Debug.Log("Added observer " + conn.address + " added for " + gameObject);

            observers[conn.connectionId] = conn;
            conn.AddToVisList(this);
        }

        static readonly HashSet<NetworkConnection> newObservers = new HashSet<NetworkConnection>();

        /// <summary>
        /// This causes the set of players that can see this object to be rebuild. The OnRebuildObservers callback function will be invoked on each NetworkBehaviour.
        /// </summary>
        /// <param name="initialize">True if this is the first time.</param>
        public void RebuildObservers(bool initialize)
        {
            if (observers == null)
                return;

            bool changed = false;
            bool result = false;

            newObservers.Clear();

            // call OnRebuildObservers function in components
            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                result |= comp.OnRebuildObservers(newObservers, initialize);
            }

            // if player connection: ensure player always see himself no matter what.
            // -> fixes https://github.com/vis2k/Mirror/issues/692 where a
            //    player might teleport out of the ProximityChecker's cast,
            //    losing the own connection as observer.
            if (connectionToClient != null && connectionToClient.isReady)
            {
                newObservers.Add(connectionToClient);
            }

            // if no component implemented OnRebuildObservers, then add all
            // connections.
            if (!result)
            {
                if (initialize)
                {
                    foreach (NetworkConnection conn in NetworkServer.connections.Values)
                    {
                        if (conn.isReady)
                            AddObserver(conn);
                    }

                    if (NetworkServer.localConnection != null && NetworkServer.localConnection.isReady)
                    {
                        AddObserver(NetworkServer.localConnection);
                    }
                }
                return;
            }

            // apply changes from rebuild
            foreach (NetworkConnection conn in newObservers)
            {
                if (conn == null)
                {
                    continue;
                }

                if (!conn.isReady)
                {
                    if (LogFilter.Debug) Debug.Log("Observer is not ready for " + gameObject + " " + conn);
                    continue;
                }

                if (initialize || !observers.ContainsKey(conn.connectionId))
                {
                    // new observer
                    conn.AddToVisList(this);
                    if (LogFilter.Debug) Debug.Log("New Observer for " + gameObject + " " + conn);
                    changed = true;
                }
            }

            foreach (NetworkConnection conn in observers.Values)
            {
                if (!newObservers.Contains(conn))
                {
                    // removed observer
                    conn.RemoveFromVisList(this, false);
                    if (LogFilter.Debug) Debug.Log("Removed Observer for " + gameObject + " " + conn);
                    changed = true;
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
                if (!newObservers.Contains(NetworkServer.localConnection))
                {
                    OnSetHostVisibility(false);
                }
            }

            if (changed)
            {
                observers.Clear();
                foreach (NetworkConnection conn in newObservers)
                {
                    if (conn.isReady)
                        observers.Add(conn.connectionId, conn);
                }
            }
        }

        /// <summary>
        /// Obsolete: Use <see cref="RemoveClientAuthority()"/> instead
        /// </summary>
        /// <param name="conn">The connection of the client to remove authority for.</param>
        /// <returns>True if authority is removed.</returns>
        [EditorBrowsable(EditorBrowsableState.Never), Obsolete("NetworkConnection parameter is no longer needed and nothing is returned")]
        public bool RemoveClientAuthority(NetworkConnection conn)
        {
            RemoveClientAuthority();
            return true;
        }

        /// <summary>
        /// Removes ownership for an object.
        /// <para>This applies to objects that had authority set by AssignClientAuthority, or <see cref="NetworkServer.Spawn">NetworkServer.Spawn</see> with a NetworkConnection parameter included.</para>
        /// <para>Authority cannot be removed for player objects.</para>
        /// </summary>
        public void RemoveClientAuthority()
        {
            if (!isServer)
            {
                Debug.LogError("RemoveClientAuthority can only be call on the server for spawned objects.");
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

                // we need to resynchronize the entire object
                // so just spawn it again,
                // the client will not create a new instance,  it will simply
                // reset all variables and remove authority
                NetworkServer.SendSpawnMessage(this, previousOwner);

                connectionToClient = null;
            }
        }

        /// <summary>
        /// Assign control of an object to a client via the client's <see cref="NetworkConnection">NetworkConnection.</see>
        /// <para>This causes hasAuthority to be set on the client that owns the object, and NetworkBehaviour.OnStartAuthority will be called on that client. This object then will be in the NetworkConnection.clientOwnedObjects list for the connection.</para>
        /// <para>Authority can be removed with RemoveClientAuthority. Only one client can own an object at any time. This does not need to be called for player objects, as their authority is setup automatically.</para>
        /// </summary>
        /// <param name="conn">	The connection of the client to assign authority to.</param>
        /// <returns>True if authority was assigned.</returns>
        public bool AssignClientAuthority(NetworkConnection conn)
        {
            if (!isServer)
            {
                Debug.LogError("AssignClientAuthority can only be called on the server for spawned objects.");
                return false;
            }

            if (connectionToClient != null && conn != connectionToClient)
            {
                Debug.LogError("AssignClientAuthority for " + gameObject + " already has an owner. Use RemoveClientAuthority() first.");
                return false;
            }

            if (conn == null)
            {
                Debug.LogError("AssignClientAuthority for " + gameObject + " owner cannot be null. Use RemoveClientAuthority() instead.");
                return false;
            }

            SetClientOwner(conn);

            // The client will match to the existing object
            // update all variables and assign authority
            NetworkServer.SendSpawnMessage(this, conn);

            clientAuthorityCallback?.Invoke(conn, this, true);

            return true;
        }

        // marks the identity for future reset, this is because we cant reset the identity during destroy
        // as people might want to be able to read the members inside OnDestroy(), and we have no way
        // of invoking reset after OnDestroy is called.
        internal void MarkForReset() => reset = true;

        // if we have marked an identity for reset we do the actual reset.
        internal void Reset()
        {
            if (!reset)
                return;

            clientStarted = false;
            reset = false;

            netId = 0;
            connectionToServer = null;
            connectionToClient = null;
            networkBehavioursCache = null;

            ClearObservers();
        }

        // MirrorUpdate is a hot path. Caching the vars msg is really worth it to
        // avoid large amounts of allocations.
        static UpdateVarsMessage varsMessage = new UpdateVarsMessage();

        // invoked by NetworkServer during Update()
        internal void MirrorUpdate()
        {
            if (observers != null && observers.Count > 0)
            {
                // one writer for owner, one for observers
                NetworkWriter ownerWriter = NetworkWriterPool.GetWriter();
                NetworkWriter observersWriter = NetworkWriterPool.GetWriter();

                // serialize all the dirty components and send (if any were dirty)
                OnSerializeAllSafely(false, ownerWriter, out int ownerWritten, observersWriter, out int observersWritten);
                if (ownerWritten > 0 || observersWritten > 0)
                {
                    // populate cached UpdateVarsMessage and send
                    varsMessage.netId = netId;

                    // send ownerWriter to owner
                    // (only if we serialized anything for owner)
                    // (only if there is a connection (e.g. if not a monster),
                    //  and if connection is ready because we use SendToReady
                    //  below too)
                    if (ownerWritten > 0)
                    {
                        varsMessage.payload = ownerWriter.ToArraySegment();
                        if (connectionToClient != null && connectionToClient.isReady)
                            NetworkServer.SendToClientOfPlayer(this, varsMessage);
                    }

                    // send observersWriter to everyone but owner
                    // (only if we serialized anything for observers)
                    if (observersWritten > 0)
                    {
                        varsMessage.payload = observersWriter.ToArraySegment();
                        NetworkServer.SendToReady(this, varsMessage, false);
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
                NetworkWriterPool.Recycle(ownerWriter);
                NetworkWriterPool.Recycle(observersWriter);
            }
            else
            {
                // clear all component's dirty bits
                ClearAllComponentsDirtyBits();
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
    }
}
