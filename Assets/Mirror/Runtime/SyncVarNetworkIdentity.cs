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
using System.Runtime.CompilerServices;

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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Utils.GetSpawnedInServerOrClient(base.Value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => base.Value = value != null ? value.netId : 0;
        }

        // OnChanged Callback is for <uint, uint>.
        // Let's also have one for <NetworkIdentity, NetworkIdentity>
        public new event Action<NetworkIdentity, NetworkIdentity> Callback;

        // overwrite CallCallback to use the NetworkIdentity version instead
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void InvokeCallback(uint oldValue, uint newValue) =>
            Callback?.Invoke(Utils.GetSpawnedInServerOrClient(oldValue), Utils.GetSpawnedInServerOrClient(newValue));

        // ctor
        // 'value = null' so we can do:
        //   SyncVarNetworkIdentity = new SyncVarNetworkIdentity()
        // instead of
        //   SyncVarNetworkIdentity = new SyncVarNetworkIdentity(null);
        public SyncVarNetworkIdentity(NetworkIdentity value = null)
            : base(value != null ? value.netId : 0) {}

        // implicit conversion: NetworkIdentity value = SyncFieldNetworkIdentity
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator NetworkIdentity(SyncVarNetworkIdentity field) => field.Value;

        // implicit conversion: SyncFieldNetworkIdentity = value
        // even if SyncField is readonly, it's still useful: SyncFieldNetworkIdentity = target;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SyncVarNetworkIdentity(NetworkIdentity value) => new SyncVarNetworkIdentity(value);

        // NOTE: overloading all == operators blocks '== null' checks with an
        // "ambiguous invocation" error. that's good. this way user code like
        // "player.target == null" won't compile instead of silently failing!

        // == operator for comparisons like Player.target==monster
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SyncVarNetworkIdentity a, SyncVarNetworkIdentity b) =>
            a.Value == b.Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SyncVarNetworkIdentity a, SyncVarNetworkIdentity b) => !(a == b);

        // == operator for comparisons like Player.target==monster
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SyncVarNetworkIdentity a, NetworkIdentity b) =>
            a.Value == b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SyncVarNetworkIdentity a, NetworkIdentity b) => !(a == b);

        // == operator for comparisons like Player.target==monster
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(NetworkIdentity a, SyncVarNetworkIdentity b) =>
            a == b.Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(NetworkIdentity a, SyncVarNetworkIdentity b) => !(a == b);

        // if we overwrite == operators, we also need to overwrite .Equals.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) => obj is SyncVarNetworkIdentity value && this == value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => Value.GetHashCode();
    }
}
