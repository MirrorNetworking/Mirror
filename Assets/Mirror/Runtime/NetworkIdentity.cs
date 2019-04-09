using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkIdentity")]
    [HelpURL("https://vis2k.github.io/Mirror/Components/NetworkIdentity")]
    public sealed class NetworkIdentity : MonoBehaviour
    {
        // configuration
        bool m_IsServer;
        NetworkBehaviour[] networkBehavioursCache;

        // member used to mark a identity for future reset
        // check MarkForReset for more information.
        bool m_Reset;

        // properties
        public bool isClient { get; internal set; }
        // dont return true if server stopped.
        public bool isServer
        {
            get => m_IsServer && NetworkServer.active && netId != 0;
            internal set => m_IsServer = value;
        }
        public bool isLocalPlayer { get; private set; }
        public bool hasAuthority { get; private set; }

        // <connectionId, NetworkConnection>
        public Dictionary<int, NetworkConnection> observers;

        public uint netId { get; internal set; }
        public ulong sceneId => m_SceneId;
        [FormerlySerializedAs("m_ServerOnly")] 
        public bool serverOnly;
        [FormerlySerializedAs("m_LocalPlayerAuthority")] 
        public bool localPlayerAuthority;
        public NetworkConnection clientAuthorityOwner { get; internal set; }
        public NetworkConnection connectionToServer { get; internal set; }
        public NetworkConnection connectionToClient { get; internal set; }

        // all spawned NetworkIdentities by netId. needed on server and client.
        public static readonly Dictionary<uint, NetworkIdentity> spawned = new Dictionary<uint, NetworkIdentity>();

        public NetworkBehaviour[] NetworkBehaviours => networkBehavioursCache = networkBehavioursCache ?? GetComponents<NetworkBehaviour>();

        // the AssetId trick:
        // - ideally we would have a serialized 'Guid m_AssetId' but Unity can't
        //   serialize it because Guid's internal bytes are private
        // - UNET used 'NetworkHash128' originally, with byte0, ..., byte16
        //   which works, but it just unnecessary extra code
        // - using just the Guid string would work, but it's 32 chars long and
        //   would then be sent over the network as 64 instead of 16 bytes
        // -> the solution is to serialize the string internally here and then
        //    use the real 'Guid' type for everything else via .assetId
        [SerializeField] string m_AssetId;
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
                else Debug.LogWarning("SetDynamicAssetId object already has an assetId <" + m_AssetId + ">");
            }
        }

        // persistent scene id <sceneHash/32,sceneId/32>
        // (see AssignSceneID comments)
        [SerializeField] ulong m_SceneId = 0;

        // keep track of all sceneIds to detect scene duplicates
        static Dictionary<ulong, NetworkIdentity> sceneIds = new Dictionary<ulong, NetworkIdentity>();

        // used when adding players
        internal void SetClientOwner(NetworkConnection conn)
        {
            if (clientAuthorityOwner != null)
            {
                Debug.LogError("SetClientOwner m_ClientAuthorityOwner already set!");
            }
            clientAuthorityOwner = conn;
            clientAuthorityOwner.AddOwnedObject(this);
        }

        internal void ForceAuthority(bool authority)
        {
            if (hasAuthority == authority)
            {
                return;
            }

            hasAuthority = authority;
            if (authority)
            {
                OnStartAuthority();
            }
            else
            {
                OnStopAuthority();
            }
        }

        static uint nextNetworkId = 1;
        internal static uint GetNextNetworkId() => nextNetworkId++;
        public static void ResetNextNetworkId() => nextNetworkId = 1;

        public delegate void ClientAuthorityCallback(NetworkConnection conn, NetworkIdentity identity, bool authorityState);
        public static ClientAuthorityCallback clientAuthorityCallback;

        // used when the player object for a connection changes
        internal void SetNotLocalPlayer()
        {
            isLocalPlayer = false;

            if (NetworkServer.active && NetworkServer.localClientActive)
            {
                // dont change authority for objects on the host
                return;
            }
            hasAuthority = false;
        }

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
            if (serverOnly && localPlayerAuthority)
            {
                Debug.LogWarning("Disabling Local Player Authority for " + gameObject + " because it is server-only.");
                localPlayerAuthority = false;
            }

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
            prefab = (GameObject)PrefabUtility.GetCorrespondingObjectFromSource(gameObject);

            if (prefab == null)
            {
                Debug.LogError("Failed to find prefab parent for scene object [name:" + gameObject.name + "]");
                return false;
            }
            return true;
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
            NetworkIdentity existing;
            bool duplicate = sceneIds.TryGetValue(m_SceneId, out existing) && existing != null && existing != this;
            if (m_SceneId == 0 || duplicate)
            {
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
                // -> exclude '0' because that's for unassigned sceneIDs
                // TODO use 0,uint.max later. Random.Range only has int version.
                m_SceneId = (uint)UnityEngine.Random.Range(1, int.MaxValue);
                Debug.Log(name + " in scene=" + gameObject.scene.name + " sceneId assigned to: " + m_SceneId.ToString("X") + (duplicate ? " because duplicated" : ""));
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
            Debug.Log(name + " in scene=" + gameObject.scene.name + " scene index hash(" + pathHash.ToString("X") + ") copied into sceneId: " + m_SceneId.ToString("X"));
        }

        void SetupIDs()
        {
            if (ThisIsAPrefab())
            {
                m_SceneId = 0; // force 0 for prefabs
                AssignAssetID(gameObject);
            }
            else if (ThisIsASceneObjectWithPrefabParent(out GameObject prefab))
            {
                AssignSceneID();
                AssignAssetID(prefab);
            }
            else if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                m_SceneId = 0; // force 0 for prefabs
                string path = PrefabStageUtility.GetCurrentPrefabStage().prefabAssetPath;
                AssignAssetID(path);
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

            if (m_IsServer && NetworkServer.active)
            {
                NetworkServer.Destroy(gameObject);
            }
        }

        internal void OnStartServer(bool allowNonZeroNetId)
        {
            if (m_IsServer)
            {
                return;
            }
            m_IsServer = true;
            hasAuthority = !localPlayerAuthority;

            observers = new Dictionary<int, NetworkConnection>();

            // If the instance/net ID is invalid here then this is an object instantiated from a prefab and the server should assign a valid ID
            if (netId == 0)
            {
                netId = GetNextNetworkId();
            }
            else
            {
                if (!allowNonZeroNetId)
                {
                    Debug.LogError("Object has non-zero netId " + netId + " for " + gameObject);
                    return;
                }
            }

            if (LogFilter.Debug) Debug.Log("OnStartServer " + this + " GUID:" + netId);

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

            if (NetworkClient.active && NetworkServer.localClientActive)
            {
                // there will be no spawn message, so start the client here too
                isClient = true;
                OnStartClient();
            }

            if (hasAuthority)
            {
                OnStartAuthority();
            }
        }

        internal void OnStartClient()
        {
            isClient = true;

            if (LogFilter.Debug) Debug.Log("OnStartClient " + gameObject + " netId:" + netId + " localPlayerAuthority:" + localPlayerAuthority);
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

        void OnStartAuthority()
        {
            if (networkBehavioursCache == null)
            {
                Debug.LogError("Network object " + name + " not initialized properly. Do you have more than one NetworkIdentity in the same object? Did you forget to spawn this object with NetworkServer?", this);
                return;
            }

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

        internal void OnSetLocalVisibility(bool vis)
        {
            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                try
                {
                    comp.OnSetLocalVisibility(vis);
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
            writer.Write((int)0);
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
                Debug.LogError("OnSerialize failed for: object=" + name + " component=" + comp.GetType() + " sceneId=" + m_SceneId.ToString("X") + "\n\n" + e.ToString());
            }
            int endPosition = writer.Position;

            // fill in length now
            writer.Position = headerPosition;
            writer.Write(endPosition - contentPosition);
            writer.Position = endPosition;

            if (LogFilter.Debug) Debug.Log("OnSerializeSafely written for object=" + comp.name + " component=" + comp.GetType() + " sceneId=" + m_SceneId.ToString("X") + "header@" + headerPosition + " content@" + contentPosition + " end@" + endPosition + " contentSize=" + (endPosition - contentPosition));

            return result;
        }

        // OnSerializeAllSafely is in hot path. caching the writer is really
        // worth it to avoid large amounts of allocations.
        static NetworkWriter onSerializeWriter = new NetworkWriter();

        // serialize all components (or only dirty ones if not initial state)
        // -> returns serialized data of everything dirty,  null if nothing was dirty
        internal byte[] OnSerializeAllSafely(bool initialState)
        {
            // reset cached writer length and position
            onSerializeWriter.SetLength(0);

            if (networkBehavioursCache.Length > 64)
            {
                Debug.LogError("Only 64 NetworkBehaviour components are allowed for NetworkIdentity: " + name + " because of the dirtyComponentMask");
                return null;
            }
            ulong dirtyComponentsMask = GetDirtyMask(networkBehavioursCache, initialState);

            if (dirtyComponentsMask == 0L)
                return null;

            onSerializeWriter.WritePackedUInt64(dirtyComponentsMask); // WritePacked64 so we don't write full 8 bytes if we don't have to

            foreach (NetworkBehaviour comp in networkBehavioursCache)
            {
                // is this component dirty?
                // -> always serialize if initialState so all components are included in spawn packet
                // -> note: IsDirty() is false if the component isn't dirty or sendInterval isn't elapsed yet
                if (initialState || comp.IsDirty())
                {
                    // serialize the data
                    if (LogFilter.Debug) Debug.Log("OnSerializeAllSafely: " + name + " -> " + comp.GetType() + " initial=" + initialState);
                    OnSerializeSafely(comp, onSerializeWriter, initialState);

                    // Clear dirty bits only if we are synchronizing data and not sending a spawn message.
                    // This preserves the behavior in HLAPI
                    if (!initialState)
                    {
                        comp.ClearAllDirtyBits();
                    }
                }
            }

            return onSerializeWriter.ToArray();
        }

        ulong GetDirtyMask(NetworkBehaviour[] components, bool initialState)
        {
            // loop through all components only once and then write dirty+payload into the writer afterwards
            ulong dirtyComponentsMask = 0L;
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

        void OnDeserializeSafely(NetworkBehaviour comp, NetworkReader reader, bool initialState)
        {
            // read header as 4 bytes
            int contentSize = reader.ReadInt32();

            // read content
            byte[] bytes = reader.ReadBytes(contentSize);
            if (LogFilter.Debug) Debug.Log("OnDeserializeSafely extracted: " + comp.name + " component=" + comp.GetType() + " sceneId=" + m_SceneId.ToString("X") + " length=" + bytes.Length);

            // call OnDeserialize with a temporary reader, so that the
            // original one can't be messed with. we also wrap it in a
            // try-catch block so there's no way to mess up another
            // component's deserialization
            try
            {
                NetworkReader componentReader = new NetworkReader(bytes);
                comp.OnDeserialize(componentReader, initialState);
                if (componentReader.Position != componentReader.Length)
                {
                    Debug.LogWarning("OnDeserialize didn't read the full " + bytes.Length + " bytes for object:" + name + " component=" + comp.GetType() + " sceneId=" + m_SceneId.ToString("X") + ". Make sure that OnSerialize and OnDeserialize write/read the same amount of data in all cases.");
                }
            }
            catch (Exception e)
            {
                // show a detailed error and let the user know what went wrong
                Debug.LogError("OnDeserialize failed for: object=" + name + " component=" + comp.GetType() + " sceneId=" + m_SceneId.ToString("X") + " length=" + bytes.Length + ". Possible Reasons:\n  * Do " + comp.GetType() + "'s OnSerialize and OnDeserialize calls write the same amount of data(" + bytes.Length +" bytes)? \n  * Was there an exception in " + comp.GetType() + "'s OnSerialize/OnDeserialize code?\n  * Are the server and client the exact same project?\n  * Maybe this OnDeserialize call was meant for another GameObject? The sceneIds can easily get out of sync if the Hierarchy was modified only in the client OR the server. Try rebuilding both.\n\n" + e.ToString());
            }
        }

        void OnDeserializeAllSafely(NetworkBehaviour[] components, NetworkReader reader, bool initialState)
        {
            // read component dirty mask
            ulong dirtyComponentsMask = reader.ReadPackedUInt64();

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

        // happens on client
        internal void HandleClientAuthority(bool authority)
        {
            if (!localPlayerAuthority)
            {
                Debug.LogError("HandleClientAuthority " + gameObject + " does not have localPlayerAuthority");
                return;
            }

            ForceAuthority(authority);
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
            if (0 <= componentIndex && componentIndex < networkBehavioursCache.Length)
            {
                NetworkBehaviour invokeComponent = networkBehavioursCache[componentIndex];
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
            OnDeserializeAllSafely(NetworkBehaviours, reader, initialState);
        }

        internal void SetLocalPlayer()
        {
            isLocalPlayer = true;

            // there is an ordering issue here that originAuthority solves. OnStartAuthority should only be called if m_HasAuthority was false when this function began,
            // or it will be called twice for this object. But that state is lost by the time OnStartAuthority is called below, so the original value is cached
            // here to be checked below.
            bool originAuthority = hasAuthority;
            if (localPlayerAuthority)
            {
                hasAuthority = true;
            }

            foreach (NetworkBehaviour comp in networkBehavioursCache)
            {
                comp.OnStartLocalPlayer();

                if (localPlayerAuthority && !originAuthority)
                {
                    comp.OnStartAuthority();
                }
            }
        }

        internal void OnNetworkDestroy()
        {
            for (int i = 0; networkBehavioursCache != null && i < networkBehavioursCache.Length; i++)
            {
                NetworkBehaviour comp = networkBehavioursCache[i];
                comp.OnNetworkDestroy();
            }
            m_IsServer = false;
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

        public void RebuildObservers(bool initialize)
        {
            if (observers == null)
                return;

            bool changed = false;
            bool result = false;
            HashSet<NetworkConnection> oldObservers = new HashSet<NetworkConnection>(observers.Values);
            HashSet<NetworkConnection> newObservers = new HashSet<NetworkConnection>();

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

                if (initialize || !oldObservers.Contains(conn))
                {
                    // new observer
                    conn.AddToVisList(this);
                    if (LogFilter.Debug) Debug.Log("New Observer for " + gameObject + " " + conn);
                    changed = true;
                }
            }

            foreach (NetworkConnection conn in oldObservers)
            {
                if (!newObservers.Contains(conn))
                {
                    // removed observer
                    conn.RemoveFromVisList(this, false);
                    if (LogFilter.Debug) Debug.Log("Removed Observer for " + gameObject + " " + conn);
                    changed = true;
                }
            }

            // special case for local client.
            if (initialize)
            {
                if (!newObservers.Contains(NetworkServer.localConnection))
                {
                    OnSetLocalVisibility(false);
                }
            }

            if (changed)
            {
                observers = newObservers.ToDictionary(conn => conn.connectionId, conn => conn);
            }
        }

        public bool RemoveClientAuthority(NetworkConnection conn)
        {
            if (!isServer)
            {
                Debug.LogError("RemoveClientAuthority can only be call on the server for spawned objects.");
                return false;
            }

            if (connectionToClient != null)
            {
                Debug.LogError("RemoveClientAuthority cannot remove authority for a player object");
                return false;
            }

            if (clientAuthorityOwner == null)
            {
                Debug.LogError("RemoveClientAuthority for " + gameObject + " has no clientAuthority owner.");
                return false;
            }

            if (clientAuthorityOwner != conn)
            {
                Debug.LogError("RemoveClientAuthority for " + gameObject + " has different owner.");
                return false;
            }

            clientAuthorityOwner.RemoveOwnedObject(this);
            clientAuthorityOwner = null;

            // server now has authority (this is only called on server)
            ForceAuthority(true);

            // send msg to that client
            ClientAuthorityMessage msg = new ClientAuthorityMessage
            {
                netId = netId,
                authority = false
            };
            conn.Send(msg);

            clientAuthorityCallback?.Invoke(conn, this, false);
            return true;
        }

        public bool AssignClientAuthority(NetworkConnection conn)
        {
            if (!isServer)
            {
                Debug.LogError("AssignClientAuthority can only be called on the server for spawned objects.");
                return false;
            }
            if (!localPlayerAuthority)
            {
                Debug.LogError("AssignClientAuthority can only be used for NetworkIdentity components with LocalPlayerAuthority set.");
                return false;
            }

            if (clientAuthorityOwner != null && conn != clientAuthorityOwner)
            {
                Debug.LogError("AssignClientAuthority for " + gameObject + " already has an owner. Use RemoveClientAuthority() first.");
                return false;
            }

            if (conn == null)
            {
                Debug.LogError("AssignClientAuthority for " + gameObject + " owner cannot be null. Use RemoveClientAuthority() instead.");
                return false;
            }

            clientAuthorityOwner = conn;
            clientAuthorityOwner.AddOwnedObject(this);

            // server no longer has authority (this is called on server). Note that local client could re-acquire authority below
            ForceAuthority(false);

            // send msg to that client
            ClientAuthorityMessage msg = new ClientAuthorityMessage
            {
                netId = netId,
                authority = true
            };
            conn.Send(msg);

            clientAuthorityCallback?.Invoke(conn, this, true);
            return true;
        }

        // marks the identity for future reset, this is because we cant reset the identity during destroy
        // as people might want to be able to read the members inside OnDestroy(), and we have no way
        // of invoking reset after OnDestroy is called.
        internal void MarkForReset() => m_Reset = true;

        // if we have marked an identity for reset we do the actual reset.
        internal void Reset()
        {
            if (!m_Reset)
                return;

            m_Reset = false;
            m_IsServer = false;
            isClient = false;
            hasAuthority = false;

            netId = 0;
            isLocalPlayer = false;
            connectionToServer = null;
            connectionToClient = null;
            networkBehavioursCache = null;

            ClearObservers();
            clientAuthorityOwner = null;
        }

        // MirrorUpdate is a hot path. Caching the vars msg is really worth it to
        // avoid large amounts of allocations.
        static UpdateVarsMessage varsMessage = new UpdateVarsMessage();

        // invoked by NetworkServer during Update()
        internal void MirrorUpdate()
        {
            // SendToReady sends to all observers. no need to serialize if we
            // don't have any.
            if (observers == null || observers.Count == 0)
                return;

            // serialize all the dirty components and send (if any were dirty)
            byte[] payload = OnSerializeAllSafely(false);
            if (payload != null)
            {
                // populate cached UpdateVarsMessage and send
                varsMessage.netId = netId;
                varsMessage.payload = payload;
                NetworkServer.SendToReady(this, varsMessage);
            }
        }
    }
}
