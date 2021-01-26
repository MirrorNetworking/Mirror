using System;
using System.Collections.Generic;
using System.ComponentModel;
using Cysharp.Threading.Tasks;
using Mirror.RemoteCalls;
using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// Sync to everyone, or only to owner.
    /// </summary>
    public enum SyncMode { Observers, Owner }

    /// <summary>
    /// Base class which should be inherited by scripts which contain networking functionality.
    /// </summary>
    /// <remarks>
    /// <para>This is a MonoBehaviour class so scripts which need to use the networking feature should inherit this class instead of MonoBehaviour. It allows you to invoke networked actions, receive various callbacks, and automatically synchronize state from server-to-client.</para>
    /// <para>The NetworkBehaviour component requires a NetworkIdentity on the game object. There can be multiple NetworkBehaviours on a single game object. For an object with sub-components in a hierarchy, the NetworkIdentity must be on the root object, and NetworkBehaviour scripts must also be on the root object.</para>
    /// <para>Some of the built-in components of the networking system are derived from NetworkBehaviour, including NetworkTransport, NetworkAnimator and NetworkProximityChecker.</para>
    /// </remarks>
    [AddComponentMenu("")]
    [HelpURL("https://mirrorng.github.io/MirrorNG/Articles/Guides/GameObjects/NetworkBehaviour.html")]
    public abstract class NetworkBehaviour : MonoBehaviour
    {
        static readonly ILogger logger = LogFactory.GetLogger(typeof(NetworkBehaviour));

        internal float lastSyncTime;

        // hidden because NetworkBehaviourInspector shows it only if has OnSerialize.
        /// <summary>
        /// sync mode for OnSerialize
        /// </summary>
        [HideInInspector] public SyncMode syncMode = SyncMode.Observers;

        // hidden because NetworkBehaviourInspector shows it only if has OnSerialize.
        /// <summary>
        /// sync interval for OnSerialize (in seconds)
        /// </summary>
        [Tooltip("Time in seconds until next change is synchronized to the client. '0' means send immediately if changed. '0.5' means only send changes every 500ms.\n(This is for state synchronization like SyncVars, SyncLists, OnSerialize. Not for Cmds, Rpcs, etc.)")]
        // [0,2] should be enough. anything >2s is too laggy anyway.
        [Range(0, 2)]
        [HideInInspector] public float syncInterval = 0.1f;

        /// <summary>
        /// Returns true if this object is active on an active server.
        /// <para>This is only true if the object has been spawned. This is different from NetworkServer.active, which is true if the server itself is active rather than this object being active.</para>
        /// </summary>
        public bool IsServer => NetIdentity.IsServer;

        /// <summary>
        /// Returns true if running as a client and this object was spawned by a server.
        /// </summary>
        public bool IsClient => NetIdentity.IsClient;

        /// <summary>
        /// Returns true if we're on host mode.
        /// </summary>
        public bool IsLocalClient => NetIdentity.IsLocalClient;

        /// <summary>
        /// This returns true if this object is the one that represents the player on the local machine.
        /// <para>In multiplayer games, there are multiple instances of the Player object. The client needs to know which one is for "themselves" so that only that player processes input and potentially has a camera attached. The IsLocalPlayer function will return true only for the player instance that belongs to the player on the local machine, so it can be used to filter out input for non-local players.</para>
        /// </summary>
        public bool IsLocalPlayer => NetIdentity.IsLocalPlayer;

        /// <summary>
        /// True if this object only exists on the server
        /// </summary>
        public bool IsServerOnly => IsServer && !IsClient;

        /// <summary>
        /// True if this object exists on a client that is not also acting as a server
        /// </summary>
        public bool IsClientOnly => IsClient && !IsServer;

        /// <summary>
        /// This returns true if this object is the authoritative version of the object in the distributed network application.
        /// <para>The <see cref="NetworkIdentity.HasAuthority">NetworkIdentity.hasAuthority</see> value on the NetworkIdentity determines how authority is determined. For most objects, authority is held by the server. For objects with <see cref="NetworkIdentity.HasAuthority">NetworkIdentity.hasAuthority</see> set, authority is held by the client of that player.</para>
        /// </summary>
        public bool HasAuthority => NetIdentity.HasAuthority;

        /// <summary>
        /// The unique network Id of this object.
        /// <para>This is assigned at runtime by the network server and will be unique for all objects for that network session.</para>
        /// </summary>
        public uint NetId => NetIdentity.NetId;

        /// <summary>
        /// The <see cref="NetworkServer">NetworkClient</see> associated to this object.
        /// </summary>
        public NetworkServer Server => NetIdentity.Server;

        public ServerObjectManager ServerObjectManager => NetIdentity.ServerObjectManager;

        /// <summary>
        /// The <see cref="NetworkClient">NetworkClient</see> associated to this object.
        /// </summary>
        public NetworkClient Client => NetIdentity.Client;

        public ClientObjectManager ClientObjectManager => NetIdentity.ClientObjectManager;

        /// <summary>
        /// The <see cref="NetworkConnection">NetworkConnection</see> associated with this <see cref="NetworkIdentity">NetworkIdentity.</see> This is only valid for player objects on the client.
        /// </summary>
        public INetworkConnection ConnectionToServer => NetIdentity.ConnectionToServer;

        /// <summary>
        /// The <see cref="NetworkConnection">NetworkConnection</see> associated with this <see cref="NetworkIdentity">NetworkIdentity.</see> This is only valid for player objects on the server.
        /// </summary>
        public INetworkConnection ConnectionToClient => NetIdentity.ConnectionToClient;

        public NetworkTime NetworkTime => IsClient ? Client.Time : Server.Time;

        protected internal ulong SyncVarDirtyBits { get; private set; }
        ulong syncVarHookGuard;

        internal protected bool GetSyncVarHookGuard(ulong dirtyBit)
        {
            return (syncVarHookGuard & dirtyBit) != 0UL;
        }

        internal protected void SetSyncVarHookGuard(ulong dirtyBit, bool value)
        {
            if (value)
                syncVarHookGuard |= dirtyBit;
            else
                syncVarHookGuard &= ~dirtyBit;
        }

        /// <summary>
        /// objects that can synchronize themselves, such as synclists
        /// </summary>
        protected readonly List<ISyncObject> syncObjects = new List<ISyncObject>();

        /// <summary>
        /// NetworkIdentity component caching for easier access
        /// </summary>
        NetworkIdentity netIdentityCache;

        /// <summary>
        /// Returns the NetworkIdentity of this object
        /// </summary>
        public NetworkIdentity NetIdentity
        {
            get
            {
                // in this specific case,  we want to know if we have set it before
                // so we can compare if the reference is null
                // instead of calling unity's MonoBehaviour == operator
                if (netIdentityCache is null)
                {
                    // GetComponentInParent doesn't works on disabled gameobjecs
                    // and GetComponentsInParent(false)[0] isn't allocation free, so
                    // we just drop child support in this specific case
                    if (gameObject.activeSelf)
                        netIdentityCache = GetComponentInParent<NetworkIdentity>();
                    else
                        netIdentityCache = GetComponent<NetworkIdentity>();

                    // do this 2nd check inside first if so that we are not checking == twice on unity Object
                    if (netIdentityCache is null)
                    {
                        logger.LogError("There is no NetworkIdentity on " + name + ". Please add one.");
                    }
                }
                return netIdentityCache;
            }
        }

        private int? componentIndex;
        /// <summary>
        /// Returns the index of the component on this object
        /// </summary>
        public int ComponentIndex
        {
            get
            {
                if (componentIndex.HasValue)
                    return componentIndex.Value;

                // note: FindIndex causes allocations, we search manually instead
                for (int i = 0; i < NetIdentity.NetworkBehaviours.Length; i++)
                {
                    NetworkBehaviour component = NetIdentity.NetworkBehaviours[i];
                    if (component == this)
                    {
                        componentIndex = i;
                        return i;
                    }
                }

                // this should never happen
                logger.LogError("Could not find component in GameObject. You should not add/remove components in networked objects dynamically", this);

                return -1;
            }
        }

        // this gets called in the constructor by the weaver
        // for every SyncObject in the component (e.g. SyncLists).
        // We collect all of them and we synchronize them with OnSerialize/OnDeserialize
        internal protected void InitSyncObject(ISyncObject syncObject)
        {
            syncObjects.Add(syncObject);
            syncObject.OnChange += SyncObject_OnChange;
        }

        private void SyncObject_OnChange()
        {
            if (IsServer)
            {
                ServerObjectManager.DirtyObjects.Add(NetIdentity);
            }
        }

        #region ServerRpcs
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected internal void SendServerRpcInternal(Type invokeClass, string cmdName, NetworkWriter writer, int channelId, bool requireAuthority = true)
        {
            ValidateServerRpc(invokeClass, cmdName, requireAuthority);

            // construct the message
            var message = new ServerRpcMessage
            {
                netId = NetId,
                componentIndex = ComponentIndex,
                // type+func so Inventory.RpcUse != Equipment.RpcUse
                functionHash = RemoteCallHelper.GetMethodHash(invokeClass, cmdName),
                // segment to avoid reader allocations
                payload = writer.ToArraySegment()
            };

            Client.SendAsync(message, channelId).Forget();
        }

        private void ValidateServerRpc(Type invokeClass, string cmdName, bool requireAuthority)
        {
            // this was in Weaver before
            // NOTE: we could remove this later to allow calling Cmds on Server
            //       to avoid Wrapper functions. a lot of people requested this.
            if (!Client.Active)
            {
                throw new InvalidOperationException($"ServerRpc Function {cmdName} called on server without an active client.");
            }

            // local players can always send ServerRpcs, regardless of authority, other objects must have authority.
            if (requireAuthority && !(IsLocalPlayer || HasAuthority))
            {
                throw new UnauthorizedAccessException($"Trying to send ServerRpc for object without authority. {invokeClass}.{cmdName}");
            }

            if (Client.Connection == null)
            {
                throw new InvalidOperationException("Send ServerRpc attempted with no client running [client=" + ConnectionToServer + "].");
            }
        }

        protected internal UniTask<T> SendServerRpcWithReturn<T>(Type invokeClass, string cmdName, NetworkWriter writer, int channelId, bool requireAuthority = true)
        {
            ValidateServerRpc(invokeClass, cmdName, requireAuthority);

            (UniTask<T> task, int id) = ClientObjectManager.CreateReplyTask<T>();

            // construct the message
            var message = new ServerRpcMessage
            {
                netId = NetId,
                componentIndex = ComponentIndex,
                replyId = id,
                // type+func so Inventory.RpcUse != Equipment.RpcUse
                functionHash = RemoteCallHelper.GetMethodHash(invokeClass, cmdName),
                // segment to avoid reader allocations
                payload = writer.ToArraySegment()
            };

            Client.SendAsync(message, channelId).Forget();

            return task;
        }

        #endregion

        #region Client RPCs
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected internal void SendRpcInternal(Type invokeClass, string rpcName, NetworkWriter writer, int channelId, bool excludeOwner)
        {
            // this was in Weaver before
            if (!Server || !Server.Active)
            {
                throw new InvalidOperationException("RPC Function " + rpcName + " called on Client.");
            }
            // This cannot use Server.active, as that is not specific to this object.
            if (!IsServer)
            {
                if (logger.WarnEnabled()) logger.LogWarning("ClientRpc " + rpcName + " called on un-spawned object: " + name);
                return;
            }

            // construct the message
            var message = new RpcMessage
            {
                netId = NetId,
                componentIndex = ComponentIndex,
                // type+func so Inventory.RpcUse != Equipment.RpcUse
                functionHash = RemoteCallHelper.GetMethodHash(invokeClass, rpcName),
                // segment to avoid reader allocations
                payload = writer.ToArraySegment()
            };

            // The public facing parameter is excludeOwner in [ClientRpc]
            // so we negate it here to logically align with SendToReady.
            bool includeOwner = !excludeOwner;
            Server.SendToObservers(NetIdentity, message, includeOwner, channelId);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected internal void SendTargetRpcInternal(INetworkConnection conn, Type invokeClass, string rpcName, NetworkWriter writer, int channelId)
        {
            // this was in Weaver before
            if (!Server || !Server.Active)
            {
                throw new InvalidOperationException("RPC Function " + rpcName + " called on client.");
            }

            // connection parameter is optional. assign if null.
            if (conn == null)
            {
                conn = ConnectionToClient;
            }

            // This cannot use Server.active, as that is not specific to this object.
            if (!IsServer)
            {
                if (logger.WarnEnabled()) logger.LogWarning("ClientRpc " + rpcName + " called on un-spawned object: " + name);
                return;
            }

            // construct the message
            var message = new RpcMessage
            {
                netId = NetId,
                componentIndex = ComponentIndex,
                // type+func so Inventory.RpcUse != Equipment.RpcUse
                functionHash = RemoteCallHelper.GetMethodHash(invokeClass, rpcName),
                // segment to avoid reader allocations
                payload = writer.ToArraySegment()
            };

            conn.Send(message, channelId);
        }
        #endregion

        #region Helpers

        [EditorBrowsable(EditorBrowsableState.Never)]
        protected internal bool SyncVarEqual<T>(T value, T fieldValue)
        {
            // newly initialized or changed value?
            return EqualityComparer<T>.Default.Equals(value, fieldValue);
        }

        #endregion

        /// <summary>
        /// Used to set the behaviour as dirty, so that a network update will be sent for the object.
        /// these are masks, not bit numbers, ie. 0x004 not 2
        /// </summary>
        /// <param name="dirtyBit">Bit mask to set.</param>
        public void SetDirtyBit(ulong dirtyBit)
        {
            SyncVarDirtyBits |= dirtyBit;
            if (IsServer)
                ServerObjectManager.DirtyObjects.Add(NetIdentity);
        }

        /// <summary>
        /// This clears all the dirty bits that were set on this script by SetDirtyBits();
        /// <para>This is automatically invoked when an update is sent for this object, but can be called manually as well.</para>
        /// </summary>
        public void ClearAllDirtyBits()
        {
            lastSyncTime = Time.time;
            SyncVarDirtyBits = 0L;

            // flush all unsynchronized changes in syncobjects
            // note: don't use List.ForEach here, this is a hot path
            //   List.ForEach: 432b/frame
            //   for: 231b/frame
            for (int i = 0; i < syncObjects.Count; ++i)
            {
                syncObjects[i].Flush();
            }
        }

        bool AnySyncObjectDirty()
        {
            // note: don't use Linq here. 1200 networked objects:
            //   Linq: 187KB GC/frame;, 2.66ms time
            //   for: 8KB GC/frame; 1.28ms time
            for (int i = 0; i < syncObjects.Count; ++i)
            {
                if (syncObjects[i].IsDirty)
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsDirty()
        {
            if (Time.time - lastSyncTime >= syncInterval)
            {
                return SyncVarDirtyBits != 0L || AnySyncObjectDirty();
            }
            return false;
        }

        // true if this component has data that has not been
        // synchronized.  Note that it may not synchronize
        // right away because of syncInterval
        public bool StillDirty()
        {
            return SyncVarDirtyBits != 0L || AnySyncObjectDirty();
        }

        /// <summary>
        /// Virtual function to override to send custom serialization data. The corresponding function to send serialization data is OnDeserialize().
        /// </summary>
        /// <remarks>
        /// <para>The initialState flag is useful to differentiate between the first time an object is serialized and when incremental updates can be sent. The first time an object is sent to a client, it must include a full state snapshot, but subsequent updates can save on bandwidth by including only incremental changes. Note that SyncVar hook functions are not called when initialState is true, only for incremental updates.</para>
        /// <para>If a class has SyncVars, then an implementation of this function and OnDeserialize() are added automatically to the class. So a class that has SyncVars cannot also have custom serialization functions.</para>
        /// <para>The OnSerialize function should return true to indicate that an update should be sent. If it returns true, then the dirty bits for that script are set to zero, if it returns false then the dirty bits are not changed. This allows multiple changes to a script to be accumulated over time and sent when the system is ready, instead of every frame.</para>
        /// </remarks>
        /// <param name="writer">Writer to use to write to the stream.</param>
        /// <param name="initialState">If this is being called to send initial state.</param>
        /// <returns>True if data was written.</returns>
        public virtual bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            bool objectWritten;
            // if initialState: write all SyncVars.
            // otherwise write dirtyBits+dirty SyncVars
            if (initialState)
            {
                objectWritten = SerializeObjectsAll(writer);
            }
            else
            {
                objectWritten = SerializeObjectsDelta(writer);
            }

            bool syncVarWritten = SerializeSyncVars(writer, initialState);

            return objectWritten || syncVarWritten;
        }


        /// <summary>
        /// Virtual function to override to receive custom serialization data. The corresponding function to send serialization data is OnSerialize().
        /// </summary>
        /// <param name="reader">Reader to read from the stream.</param>
        /// <param name="initialState">True if being sent initial state.</param>
        public virtual void OnDeserialize(NetworkReader reader, bool initialState)
        {
            if (initialState)
            {
                DeSerializeObjectsAll(reader);
            }
            else
            {
                DeSerializeObjectsDelta(reader);
            }

            DeserializeSyncVars(reader, initialState);
        }

        // Don't rename. Weaver uses this exact function name.
        public virtual bool SerializeSyncVars(NetworkWriter writer, bool initialState)
        {
            return false;

            // SyncVar are writen here in subclass

            // if initialState
            //   write all SyncVars
            // else
            //   write syncVarDirtyBits
            //   write dirty SyncVars
        }

        // Don't rename. Weaver uses this exact function name.
        public virtual void DeserializeSyncVars(NetworkReader reader, bool initialState)
        {
            // SyncVars are read here in subclass

            // if initialState
            //   read all SyncVars
            // else
            //   read syncVarDirtyBits
            //   read dirty SyncVars
        }

        internal ulong DirtyObjectBits()
        {
            ulong dirtyObjects = 0;
            for (int i = 0; i < syncObjects.Count; i++)
            {
                ISyncObject syncObject = syncObjects[i];
                if (syncObject.IsDirty)
                {
                    dirtyObjects |= 1UL << i;
                }
            }
            return dirtyObjects;
        }

        public bool SerializeObjectsAll(NetworkWriter writer)
        {
            bool dirty = false;
            for (int i = 0; i < syncObjects.Count; i++)
            {
                ISyncObject syncObject = syncObjects[i];
                syncObject.OnSerializeAll(writer);
                dirty = true;
            }
            return dirty;
        }

        public bool SerializeObjectsDelta(NetworkWriter writer)
        {
            bool dirty = false;
            // write the mask
            writer.WritePackedUInt64(DirtyObjectBits());
            // serializable objects, such as synclists
            for (int i = 0; i < syncObjects.Count; i++)
            {
                ISyncObject syncObject = syncObjects[i];
                if (syncObject.IsDirty)
                {
                    syncObject.OnSerializeDelta(writer);
                    dirty = true;
                }
            }
            return dirty;
        }

        internal void DeSerializeObjectsAll(NetworkReader reader)
        {
            for (int i = 0; i < syncObjects.Count; i++)
            {
                ISyncObject syncObject = syncObjects[i];
                syncObject.OnDeserializeAll(reader);
            }
        }

        internal void DeSerializeObjectsDelta(NetworkReader reader)
        {
            ulong dirty = reader.ReadPackedUInt64();
            for (int i = 0; i < syncObjects.Count; i++)
            {
                ISyncObject syncObject = syncObjects[i];
                if ((dirty & (1UL << i)) != 0)
                {
                    syncObject.OnDeserializeDelta(reader);
                }
            }
        }

        internal void ResetSyncObjects()
        {
            foreach (ISyncObject syncObject in syncObjects)
            {
                syncObject.Reset();
            }
        }
    }
}
