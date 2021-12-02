// persistent NetworkIdentity SyncField which stores .netId internally.
// this is necessary for cases like a player's target.
// the target might run in and out of visibility range and become 'null'.
// but the 'netId' remains and will always point to the monster if around.
//
// original Weaver code with netId workaround:
/*
    // USER:
    [SyncVar(hook = "OnTargetChanged")]
    public NetworkIdentity target;

    // WEAVER GENERATED:
    private uint ___targetNetId;

    public NetworkIdentity Networktarget
    {
        get
        {
            return GetSyncVarNetworkIdentity(___targetNetId, ref target);
        }
        [param: In]
        set
        {
            if (!SyncVarNetworkIdentityEqual(value, ___targetNetId))
            {
                NetworkIdentity networktarget = Networktarget;
                SetSyncVarNetworkIdentity(value, ref target, 1uL, ref ___targetNetId);
                if (NetworkServer.localClientActive && !GetSyncVarHookGuard(1uL))
                {
                    SetSyncVarHookGuard(1uL, value: true);
                    OnTargetChanged(networktarget, value);
                    SetSyncVarHookGuard(1uL, value: false);
                }
            }
        }
    }
*/
using System;

namespace Mirror
{
    // SyncField<NetworkIdentity> only stores an uint netId.
    // while providing .spawned lookup for convenience.
    // NOTE: server always knows all spawned. consider caching the field again.
    public class SyncVarNetworkIdentity : SyncVar<uint>
    {
        // .spawned lookup from netId overwrites base uint .Value
        public new NetworkIdentity Value
        {
            get => Utils.GetSpawnedInServerOrClient(base.Value);
            set => base.Value = value != null ? value.netId : 0;
        }

        // ctor
        // 'value = null' so we can do:
        //   SyncVarNetworkIdentity = new SyncVarNetworkIdentity()
        // instead of
        //   SyncVarNetworkIdentity = new SyncVarNetworkIdentity(null);
        public SyncVarNetworkIdentity(NetworkIdentity value = null, Action<NetworkIdentity, NetworkIdentity> hook = null)
            : base(value != null ? value.netId : 0,
                hook != null ? WrapHook(hook) : null) {}

        // implicit conversion: NetworkIdentity value = SyncFieldNetworkIdentity
        public static implicit operator NetworkIdentity(SyncVarNetworkIdentity field) => field.Value;

        // implicit conversion: SyncFieldNetworkIdentity = value
        // even if SyncField is readonly, it's still useful: SyncFieldNetworkIdentity = target;
        public static implicit operator SyncVarNetworkIdentity(NetworkIdentity value) => new SyncVarNetworkIdentity(value);

        // wrap <NetworkIdentity> hook within base <uint> hook
        static Action<uint, uint> WrapHook(Action<NetworkIdentity, NetworkIdentity> hook) =>
            (oldValue, newValue) => { hook(Utils.GetSpawnedInServerOrClient(oldValue), Utils.GetSpawnedInServerOrClient(newValue)); };
    }
}
