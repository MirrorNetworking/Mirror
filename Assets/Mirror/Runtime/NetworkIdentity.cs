using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

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
    public sealed class NetworkIdentity : MonoBehaviour
    {
        // configuration
        [SerializeField] uint m_SceneId;
        [SerializeField] bool m_ServerOnly;
        [SerializeField] bool m_LocalPlayerAuthority;

        // runtime data
        bool m_IsClient;
        bool m_IsServer;
        bool m_HasAuthority;

        uint m_NetId;
        bool m_IsLocalPlayer;
        NetworkConnection m_ConnectionToServer;
        NetworkConnection m_ConnectionToClient;
        NetworkBehaviour[] m_NetworkBehaviours;


        NetworkConnection m_ClientAuthorityOwner;

        // member used to mark a identity for future reset
        // check MarkForReset for more information.
        bool m_Reset;

        // properties
        public bool isClient { get { return m_IsClient; } }
        public bool isServer { get { return m_IsServer && NetworkServer.active; } } // dont return true if server stopped.
        public bool isLocalPlayer { get { return m_IsLocalPlayer; } }
        public bool hasAuthority { get { return m_HasAuthority; } }

        // <connectionId, NetworkConnection>
        public Dictionary<int, NetworkConnection> observers;

        public uint netId { get { return m_NetId; } }
        public uint sceneId { get { return m_SceneId; } }
        public bool serverOnly { get { return m_ServerOnly; } set { m_ServerOnly = value; } }
        public bool localPlayerAuthority { get { return m_LocalPlayerAuthority; } set { m_LocalPlayerAuthority = value; } }
        public NetworkConnection clientAuthorityOwner { get { return m_ClientAuthorityOwner; }}
        public NetworkConnection connectionToServer { get { return m_ConnectionToServer; } }
        public NetworkConnection connectionToClient { get { return m_ConnectionToClient; } }

        // all spawned NetworkIdentities by netId. needed on server and client.
        public static Dictionary<uint, NetworkIdentity> spawned = new Dictionary<uint, NetworkIdentity>();

        public NetworkBehaviour[] NetworkBehaviours
        {
            get
            {
                m_NetworkBehaviours = m_NetworkBehaviours ?? GetComponents<NetworkBehaviour>();
                return m_NetworkBehaviours;
            }
        }

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
        }

        internal void SetDynamicAssetId(Guid newAssetId)
        {
            string newAssetIdString = newAssetId.ToString("N");
            if (string.IsNullOrEmpty(m_AssetId) || m_AssetId == newAssetIdString)
            {
                m_AssetId = newAssetIdString;
            }
            else
            {
                Debug.LogWarning("SetDynamicAssetId object already has an assetId <" + m_AssetId + ">");
            }
        }

        // used when adding players
        internal void SetClientOwner(NetworkConnection conn)
        {
            if (m_ClientAuthorityOwner != null)
            {
                Debug.LogError("SetClientOwner m_ClientAuthorityOwner already set!");
            }
            m_ClientAuthorityOwner = conn;
            m_ClientAuthorityOwner.AddOwnedObject(this);
        }

        // used during dispose after disconnect
        internal void ClearClientOwner()
        {
            m_ClientAuthorityOwner = null;
        }

        internal void ForceAuthority(bool authority)
        {
            if (m_HasAuthority == authority)
            {
                return;
            }

            m_HasAuthority = authority;
            if (authority)
            {
                OnStartAuthority();
            }
            else
            {
                OnStopAuthority();
            }
        }

        static uint s_NextNetworkId = 1;
        internal static uint GetNextNetworkId()
        {
            return s_NextNetworkId++;
        }

        public delegate void ClientAuthorityCallback(NetworkConnection conn, NetworkIdentity identity, bool authorityState);
        public static ClientAuthorityCallback clientAuthorityCallback;

        // only used during spawning on clients to set the identity.
        internal void SetNetworkInstanceId(uint newNetId)
        {
            m_NetId = newNetId;
            if (newNetId == 0)
            {
                m_IsServer = false;
            }
        }

        // only used when fixing duplicate scene IDs during post-processing
        public void ForceSceneId(uint newSceneId)
        {
            m_SceneId = newSceneId;
        }

        internal void EnableIsClient()
        {
            m_IsClient = true;
        }

        internal void EnableIsServer()
        {
            m_IsServer = true;
        }

        // used when the player object for a connection changes
        internal void SetNotLocalPlayer()
        {
            m_IsLocalPlayer = false;

            if (NetworkServer.active && NetworkServer.localClientActive)
            {
                // dont change authority for objects on the host
                return;
            }
            m_HasAuthority = false;
        }

        // this is used when a connection is destroyed, since the "observers" property is read-only
        internal void RemoveObserverInternal(NetworkConnection conn)
        {
            if (observers != null)
            {
                observers.Remove(conn.connectionId);
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (m_ServerOnly && m_LocalPlayerAuthority)
            {
                Debug.LogWarning("Disabling Local Player Authority for " + gameObject + " because it is server-only.");
                m_LocalPlayerAuthority = false;
            }

            SetupIDs();
        }

        void AssignAssetID(GameObject prefab)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            AssignAssetID(path);
        }

        void AssignAssetID(string path)
        {
            m_AssetId = AssetDatabase.AssetPathToGUID(path);
        }

        bool ThisIsAPrefab()
        {
#if UNITY_2018_3_OR_NEWER
            return PrefabUtility.IsPartOfPrefabAsset(gameObject);
#else
            PrefabType prefabType = PrefabUtility.GetPrefabType(gameObject);
            if (prefabType == PrefabType.Prefab)
                return true;
            return false;
#endif
        }

        bool ThisIsASceneObjectWithPrefabParent(out GameObject prefab)
        {
            prefab = null;

#if UNITY_2018_3_OR_NEWER
            if (!PrefabUtility.IsPartOfPrefabInstance(gameObject))
            {
                return false;
            }
            prefab = (GameObject)PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
#elif UNITY_2018_2_OR_NEWER
            PrefabType prefabType = PrefabUtility.GetPrefabType(gameObject);
            if (prefabType == PrefabType.None)
                return false;
            prefab = (GameObject)PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
#else
            PrefabType prefabType = PrefabUtility.GetPrefabType(gameObject);
            if (prefabType == PrefabType.None)
                return false;
            prefab = (GameObject)PrefabUtility.GetPrefabParent(gameObject);
#endif

            if (prefab == null)
            {
                Debug.LogError("Failed to find prefab parent for scene object [name:" + gameObject.name + "]");
                return false;
            }
            return true;
        }

        void SetupIDs()
        {
            GameObject prefab;
            if (ThisIsAPrefab())
            {
                ForceSceneId(0);
                AssignAssetID(gameObject);
            }
            else if (ThisIsASceneObjectWithPrefabParent(out prefab))
            {
                AssignAssetID(prefab);
            }
#if UNITY_2018_3_OR_NEWER
            else if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                ForceSceneId(0);
                string path = PrefabStageUtility.GetCurrentPrefabStage().prefabAssetPath;
                AssignAssetID(path);
            }
#endif
            else
            {
                m_AssetId = "";
            }
        }

#endif
        void OnDestroy()
        {
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
            m_HasAuthority = !m_LocalPlayerAuthority;

            observers = new Dictionary<int, NetworkConnection>();

            // If the instance/net ID is invalid here then this is an object instantiated from a prefab and the server should assign a valid ID
            if (netId == 0)
            {
                m_NetId = GetNextNetworkId();
            }
            else
            {
                if (!allowNonZeroNetId)
                {
                    Debug.LogError("Object has non-zero netId " + netId + " for " + gameObject);
                    return;
                }
            }

            if (LogFilter.Debug) { Debug.Log("OnStartServer " + this + " GUID:" + netId); }

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
                EnableIsClient();
                OnStartClient();
            }

            if (m_HasAuthority)
            {
                OnStartAuthority();
            }
        }

        internal void OnStartClient()
        {
            m_IsClient = true;

            if (LogFilter.Debug) { Debug.Log("OnStartClient " + gameObject + " GUID:" + netId + " localPlayerAuthority:" + localPlayerAuthority); }
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

        internal void OnStartAuthority()
        {
            if (m_NetworkBehaviours == null)
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

        internal void OnStopAuthority()
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

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        // vis2k: readstring bug prevention: https://issuetracker.unity3d.com/issues/unet-networkwriter-dot-write-causing-readstring-slash-readbytes-out-of-range-errors-in-clients
        // -> OnSerialize writes length,componentData,length,componentData,...
        // -> OnDeserialize carefully extracts each data, then deserializes each component with separate readers
        //    -> it will be impossible to read too many or too few bytes in OnDeserialize
        //    -> we can properly track down errors
        internal bool OnSerializeSafely(NetworkBehaviour comp, NetworkWriter writer, bool initialState)
        {
            // serialize into a temporary writer
            NetworkWriter temp = new NetworkWriter();
            bool result = false;
            try
            {
                result = comp.OnSerialize(temp, initialState);
            }
            catch (Exception e)
            {
                // show a detailed error and let the user know what went wrong
                Debug.LogError("OnSerialize failed for: object=" + name + " component=" + comp.GetType() + " sceneId=" + m_SceneId + "\n\n" + e.ToString());
            }
            byte[] bytes = temp.ToArray();
            if (LogFilter.Debug) { Debug.Log("OnSerializeSafely written for object=" + comp.name + " component=" + comp.GetType() + " sceneId=" + m_SceneId + " length=" + bytes.Length); }

            // original HLAPI had a warning in UNetUpdate() in case of large state updates. let's move it here, might
            // be useful for debugging.
            if (bytes.Length > NetworkManager.singleton.transport.GetMaxPacketSize())
            {
                Debug.LogWarning("Large state update of " + bytes.Length + " bytes for netId:" + netId + " from script:" + comp);
            }

            // serialize length,data into the real writer, untouched by user code
            writer.WriteBytesAndSize(bytes);
            return result;
        }

        // serialize all components (or only dirty ones if not initial state)
        // -> returns serialized data of everything dirty,  null if nothing was dirty
        internal byte[] OnSerializeAllSafely(bool initialState)
        {
            if (m_NetworkBehaviours.Length > 64)
            {
                Debug.LogError("Only 64 NetworkBehaviour components are allowed for NetworkIdentity: " + name + " because of the dirtyComponentMask");
                return null;
            }
            ulong dirtyComponentsMask = GetDirtyMask(m_NetworkBehaviours, initialState);

            if (dirtyComponentsMask == 0L)
                return null;

            NetworkWriter writer = new NetworkWriter();
            writer.WritePackedUInt64(dirtyComponentsMask); // WritePacked64 so we don't write full 8 bytes if we don't have to

            foreach (NetworkBehaviour comp in m_NetworkBehaviours)
            {
                // is this component dirty?
                // -> always serialize if initialState so all components are included in spawn packet
                // -> note: IsDirty() is false if the component isn't dirty or sendInterval isn't elapsed yet
                if (initialState || comp.IsDirty())
                {
                    // serialize the data
                    if (LogFilter.Debug) { Debug.Log("OnSerializeAllSafely: " + name + " -> " + comp.GetType() + " initial=" + initialState); }
                    OnSerializeSafely(comp, writer, initialState);

                    // Clear dirty bits only if we are synchronizing data and not sending a spawn message.
                    // This preserves the behavior in HLAPI
                    if (!initialState)
                    {
                        comp.ClearAllDirtyBits();
                    }
                }
            }

            return writer.ToArray();
        }

        private ulong GetDirtyMask(NetworkBehaviour[] components, bool initialState)
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

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        internal void OnDeserializeSafely(NetworkBehaviour comp, NetworkReader reader, bool initialState)
        {
            // extract data length and data safely, untouched by user code
            // -> returns empty array if length is 0, so .Length is always the proper length
            byte[] bytes = reader.ReadBytesAndSize();
            if (LogFilter.Debug) { Debug.Log("OnDeserializeSafely extracted: " + comp.name + " component=" + comp.GetType() + " sceneId=" + m_SceneId + " length=" + bytes.Length); }

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
                    Debug.LogWarning("OnDeserialize didn't read the full " + bytes.Length + " bytes for object:" + name + " component=" + comp.GetType() + " sceneId=" + m_SceneId + ". Make sure that OnSerialize and OnDeserialize write/read the same amount of data in all cases.");
                }
            }
            catch (Exception e)
            {
                // show a detailed error and let the user know what went wrong
                Debug.LogError("OnDeserialize failed for: object=" + name + " component=" + comp.GetType() + " sceneId=" + m_SceneId + " length=" + bytes.Length + ". Possible Reasons:\n  * Do " + comp.GetType() + "'s OnSerialize and OnDeserialize calls write the same amount of data(" + bytes.Length +" bytes)? \n  * Was there an exception in " + comp.GetType() + "'s OnSerialize/OnDeserialize code?\n  * Are the server and client the exact same project?\n  * Maybe this OnDeserialize call was meant for another GameObject? The sceneIds can easily get out of sync if the Hierarchy was modified only in the client OR the server. Try rebuilding both.\n\n" + e.ToString());
            }
        }

        internal void OnDeserializeAllSafely(NetworkBehaviour[] components, NetworkReader reader, bool initialState)
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

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

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
        internal void HandleRemoteCall(int componentIndex, int functionHash, UNetInvokeType invokeType, NetworkReader reader)
        {
            if (gameObject == null)
            {
                Debug.LogWarning(invokeType + " [" + functionHash + "] received for deleted object [netId=" + netId + "]");
                return;
            }

            // find the right component to invoke the function on
            if (0 <= componentIndex && componentIndex < m_NetworkBehaviours.Length)
            {
                NetworkBehaviour invokeComponent = m_NetworkBehaviours[componentIndex];
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
            HandleRemoteCall(componentIndex, eventHash, UNetInvokeType.SyncEvent, reader);
        }

        // happens on server
        internal void HandleCommand(int componentIndex, int cmdHash, NetworkReader reader)
        {
            HandleRemoteCall(componentIndex, cmdHash, UNetInvokeType.Command, reader);
        }

        // happens on client
        internal void HandleRPC(int componentIndex, int rpcHash, NetworkReader reader)
        {
            HandleRemoteCall(componentIndex, rpcHash, UNetInvokeType.ClientRpc, reader);
        }

        internal void OnUpdateVars(NetworkReader reader, bool initialState)
        {
            if (initialState && m_NetworkBehaviours == null)
            {
                m_NetworkBehaviours = GetComponents<NetworkBehaviour>();
            }

            OnDeserializeAllSafely(m_NetworkBehaviours, reader, initialState);
        }

        internal void SetLocalPlayer()
        {
            m_IsLocalPlayer = true;

            // there is an ordering issue here that originAuthority solves. OnStartAuthority should only be called if m_HasAuthority was false when this function began,
            // or it will be called twice for this object. But that state is lost by the time OnStartAuthority is called below, so the original value is cached
            // here to be checked below.
            bool originAuthority = m_HasAuthority;
            if (localPlayerAuthority)
            {
                m_HasAuthority = true;
            }

            for (int i = 0; i < m_NetworkBehaviours.Length; i++)
            {
                NetworkBehaviour comp = m_NetworkBehaviours[i];
                comp.OnStartLocalPlayer();

                if (localPlayerAuthority && !originAuthority)
                {
                    comp.OnStartAuthority();
                }
            }
        }

        internal void SetConnectionToServer(NetworkConnection conn)
        {
            m_ConnectionToServer = conn;
        }

        internal void SetConnectionToClient(NetworkConnection conn)
        {
            m_ConnectionToClient = conn;
        }

        internal void OnNetworkDestroy()
        {
            for (int i = 0; m_NetworkBehaviours != null && i < m_NetworkBehaviours.Length; i++)
            {
                NetworkBehaviour comp = m_NetworkBehaviours[i];
                comp.OnNetworkDestroy();
            }
            m_IsServer = false;
        }

        internal void ClearObservers()
        {
            if (observers != null)
            {
                foreach (KeyValuePair<int, NetworkConnection> kvp in observers)
                {
                    kvp.Value.RemoveFromVisList(this, true);
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

            if (LogFilter.Debug) { Debug.Log("Added observer " + conn.address + " added for " + gameObject); }

            observers[conn.connectionId] = conn;
            conn.AddToVisList(this);
        }

        internal void RemoveObserver(NetworkConnection conn)
        {
            if (observers == null)
                return;

            observers.Remove(conn.connectionId);
            conn.RemoveFromVisList(this, false);
        }

        public void RebuildObservers(bool initialize)
        {
            if (observers == null)
                return;

            bool changed = false;
            bool result = false;
            HashSet<NetworkConnection> newObservers = new HashSet<NetworkConnection>();
            HashSet<NetworkConnection> oldObservers = new HashSet<NetworkConnection>(observers.Values);

            foreach (NetworkBehaviour comp in NetworkBehaviours)
            {
                result |= comp.OnRebuildObservers(newObservers, initialize);
            }
            if (!result)
            {
                // none of the behaviours rebuilt our observers, use built-in rebuild method
                if (initialize)
                {
                    foreach (KeyValuePair<int, NetworkConnection> kvp in NetworkServer.connections)
                    {
                        NetworkConnection conn = kvp.Value;
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
                    Debug.LogWarning("Observer is not ready for " + gameObject + " " + conn);
                    continue;
                }

                if (initialize || !oldObservers.Contains(conn))
                {
                    // new observer
                    conn.AddToVisList(this);
                    if (LogFilter.Debug) { Debug.Log("New Observer for " + gameObject + " " + conn); }
                    changed = true;
                }
            }

            foreach (NetworkConnection conn in oldObservers)
            {
                if (!newObservers.Contains(conn))
                {
                    // removed observer
                    conn.RemoveFromVisList(this, false);
                    if (LogFilter.Debug) { Debug.Log("Removed Observer for " + gameObject + " " + conn); }
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

            if (m_ClientAuthorityOwner == null)
            {
                Debug.LogError("RemoveClientAuthority for " + gameObject + " has no clientAuthority owner.");
                return false;
            }

            if (m_ClientAuthorityOwner != conn)
            {
                Debug.LogError("RemoveClientAuthority for " + gameObject + " has different owner.");
                return false;
            }

            m_ClientAuthorityOwner.RemoveOwnedObject(this);
            m_ClientAuthorityOwner = null;

            // server now has authority (this is only called on server)
            ForceAuthority(true);

            // send msg to that client
            var msg = new ClientAuthorityMessage();
            msg.netId = netId;
            msg.authority = false;
            conn.Send((short)MsgType.LocalClientAuthority, msg);

            if (clientAuthorityCallback != null)
            {
                clientAuthorityCallback(conn, this, false);
            }
            return true;
        }

        public bool AssignClientAuthority(NetworkConnection conn)
        {
            if (!isServer)
            {
                Debug.LogError("AssignClientAuthority can only be call on the server for spawned objects.");
                return false;
            }
            if (!localPlayerAuthority)
            {
                Debug.LogError("AssignClientAuthority can only be used for NetworkIdentity component with LocalPlayerAuthority set.");
                return false;
            }

            if (m_ClientAuthorityOwner != null && conn != m_ClientAuthorityOwner)
            {
                Debug.LogError("AssignClientAuthority for " + gameObject + " already has an owner. Use RemoveClientAuthority() first.");
                return false;
            }

            if (conn == null)
            {
                Debug.LogError("AssignClientAuthority for " + gameObject + " owner cannot be null. Use RemoveClientAuthority() instead.");
                return false;
            }

            m_ClientAuthorityOwner = conn;
            m_ClientAuthorityOwner.AddOwnedObject(this);

            // server no longer has authority (this is called on server). Note that local client could re-acquire authority below
            ForceAuthority(false);

            // send msg to that client
            var msg = new ClientAuthorityMessage();
            msg.netId = netId;
            msg.authority = true;
            conn.Send((short)MsgType.LocalClientAuthority, msg);

            if (clientAuthorityCallback != null)
            {
                clientAuthorityCallback(conn, this, true);
            }
            return true;
        }

        // marks the identity for future reset, this is because we cant reset the identity during destroy
        // as people might want to be able to read the members inside OnDestroy(), and we have no way
        // of invoking reset after OnDestroy is called.
        internal void MarkForReset()
        {
            m_Reset = true;
        }

        // if we have marked an identity for reset we do the actual reset.
        internal void Reset()
        {
            if (!m_Reset)
                return;

            m_Reset = false;
            m_IsServer = false;
            m_IsClient = false;
            m_HasAuthority = false;

            m_NetId = 0;
            m_IsLocalPlayer = false;
            m_ConnectionToServer = null;
            m_ConnectionToClient = null;
            m_NetworkBehaviours = null;

            ClearObservers();
            m_ClientAuthorityOwner = null;
        }

        // invoked by unity runtime immediately after the regular "Update()" function.
        internal void UNetUpdate()
        {
            // SendToReady sends to all observers. no need to serialize if we
            // don't have any.
            if (observers == null || observers.Count == 0)
                return;

            // serialize all the dirty components and send (if any were dirty)
            byte[] payload = OnSerializeAllSafely(false);
            if (payload != null)
            {
                // construct message and send
                UpdateVarsMessage message = new UpdateVarsMessage();
                message.netId = netId;
                message.payload = payload;

                NetworkServer.SendToReady(this, (short)MsgType.UpdateVars, message);
            }
        }

        // this is invoked by the UnityEngine
        public static void UNetStaticUpdate()
        {
            NetworkServer.Update();
            NetworkClient.UpdateClients();
            NetworkManager.UpdateScene();
        }
    }
}
