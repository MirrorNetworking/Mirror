// persistent NetworkBehaviour SyncField which stores netId and component index.
// this is necessary for cases like a player's target.
// the target might run in and out of visibility range and become 'null'.
// but the 'netId' remains and will always point to the monster if around.
// (we also store the component index because GameObject can have multiple
//  NetworkBehaviours of same type)
//
// original Weaver code with netId workaround:
/*
    // USER
    [SyncVar(hook = "OnTargetChanged")]
    public NetworkBehaviour target;

    // WEAVER
    public NetworkBehaviour Networktarget
    {
        get
        {
            return target;
        }
        [param: In]
        set
        {
            if (!NetworkBehaviour.SyncVarEqual(value, ref target))
            {
                NetworkBehaviour old = target;
                SetSyncVar(value, ref target, 1uL);
                if (NetworkServer.localClientActive && !GetSyncVarHookGuard(1uL))
                {
                    SetSyncVarHookGuard(1uL, value: true);
                    OnTargetChanged(old, value);
                    SetSyncVarHookGuard(1uL, value: false);
                }
            }
        }
    }

    // SERIALIZATION
    public override bool SerializeSyncVars(NetworkWriter writer, bool forceAll)
    {
        bool result = base.SerializeSyncVars(writer, forceAll);
        if (forceAll)
        {
            writer.WriteNetworkBehaviour(target);
            return true;
        }
        writer.WriteULong(base.syncVarDirtyBits);
        if ((base.syncVarDirtyBits & 1L) != 0L)
        {
            writer.WriteNetworkBehaviour(target);
            result = true;
        }
        return result;
    }

    public override void DeserializeSyncVars(NetworkReader reader, bool initialState)
    {
        base.DeserializeSyncVars(reader, initialState);
        if (initialState)
        {
            NetworkBehaviour networkBehaviour = target;
            Networktarget = reader.ReadNetworkBehaviour();
            if (!NetworkBehaviour.SyncVarEqual(networkBehaviour, ref target))
            {
                OnTargetChanged(networkBehaviour, target);
            }
            return;
        }
        long num = (long)reader.ReadULong();
        if ((num & 1L) != 0L)
        {
            NetworkBehaviour networkBehaviour2 = target;
            Networktarget = reader.ReadNetworkBehaviour();
            if (!NetworkBehaviour.SyncVarEqual(networkBehaviour2, ref target))
            {
                OnTargetChanged(networkBehaviour2, target);
            }
        }
    }
*/
using System;

namespace Mirror
{
    // SyncField<NetworkBehaviour> stores an uint netId.
    // while providing .spawned lookup for convenience.
    // NOTE: server always knows all spawned. consider caching the field again.
    /*public class SyncFieldNetworkBehaviour : SyncField<uint>
    {
        // .spawned lookup from netId overwrites base uint .Value
        public new NetworkBehaviour Value
        {
            get => null;
            set {}
        }

        // ctor
        public SyncFieldNetworkBehaviour(NetworkBehaviour value, Action<NetworkBehaviour, NetworkBehaviour> hook = null)
            : base(value != null ? value.netId : 0,
                   hook != null ? WrapHook(hook) : null) {}

    }*/
}
