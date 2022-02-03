// persistent GameObject SyncField which stores .netId internally.
// this is necessary for cases like a player's target.
// the target might run in and out of visibility range and become 'null'.
// but the 'netId' remains and will always point to the monster if around.
//
// NOTE that SyncFieldNetworkIdentity is faster (no .gameObject/GetComponent<>)!
//
// original Weaver code with netId workaround:
/*
    // USER:
    [SyncVar(hook = "OnTargetChanged")]
    public GameObject target;

    // WEAVER:
    private uint ___targetNetId;

    public GameObject Networktarget
    {
        get
        {
            return GetSyncVarGameObject(___targetNetId, ref target);
        }
        [param: In]
        set
        {
            if (!NetworkBehaviour.SyncVarGameObjectEqual(value, ___targetNetId))
            {
                GameObject networktarget = Networktarget;
                SetSyncVarGameObject(value, ref target, 1uL, ref ___targetNetId);
                if (NetworkServer.localClientActive && !GetSyncVarHookGuard(1uL))
                {
                    SetSyncVarHookGuard(1uL, value: true);
                    OnTargetChanged(networktarget, value);
                    SetSyncVarHookGuard(1uL, value: false);
                }
            }
        }
    }

    private void OnTargetChanged(GameObject old, GameObject value)
    {
    }
*/
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror
{
    // SyncField<GameObject> only stores an uint netId.
    // while providing .spawned lookup for convenience.
    // NOTE: server always knows all spawned. consider caching the field again.
    public class SyncVarGameObject : SyncVar<uint>
    {
        // .spawned lookup from netId overwrites base uint .Value
        public new GameObject Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => GetGameObject(base.Value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => base.Value = GetNetId(value);
        }

        // OnChanged Callback is for <uint, uint>.
        // Let's also have one for <GameObject, GameObject>
        public new event Action<GameObject, GameObject> Callback;

        // overwrite CallCallback to use the GameObject version instead
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void InvokeCallback(uint oldValue, uint newValue) =>
            Callback?.Invoke(GetGameObject(oldValue), GetGameObject(newValue));

        // ctor
        // 'value = null' so we can do:
        //   SyncVarGameObject = new SyncVarGameObject()
        // instead of
        //   SyncVarGameObject = new SyncVarGameObject(null);
        public SyncVarGameObject(GameObject value = null)
            : base(GetNetId(value)) {}

        // helper function to get netId from GameObject (if any)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint GetNetId(GameObject go)
        {
            if (go != null)
            {
                NetworkIdentity identity = go.GetComponent<NetworkIdentity>();
                return identity != null ? identity.netId : 0;
            }
            return 0;
        }

        // helper function to get GameObject from netId (if spawned)
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static GameObject GetGameObject(uint netId)
        {
            NetworkIdentity spawned = Utils.GetSpawnedInServerOrClient(netId);
            return spawned != null ? spawned.gameObject : null;
        }

        // implicit conversion: GameObject value = SyncFieldGameObject
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator GameObject(SyncVarGameObject field) => field.Value;

        // implicit conversion: SyncFieldGameObject = value
        // even if SyncField is readonly, it's still useful: SyncFieldGameObject = target;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SyncVarGameObject(GameObject value) => new SyncVarGameObject(value);

        // == operator for comparisons like Player.target==monster
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SyncVarGameObject a, SyncVarGameObject b) =>
            a.Value == b.Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SyncVarGameObject a, SyncVarGameObject b) => !(a == b);

        // NOTE: overloading all == operators blocks '== null' checks with an
        // "ambiguous invocation" error. that's good. this way user code like
        // "player.target == null" won't compile instead of silently failing!

        // == operator for comparisons like Player.target==monster
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SyncVarGameObject a, GameObject b) =>
            a.Value == b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SyncVarGameObject a, GameObject b) => !(a == b);

        // == operator for comparisons like Player.target==monster
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(GameObject a, SyncVarGameObject b) =>
            a == b.Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(GameObject a, SyncVarGameObject b) => !(a == b);

        // if we overwrite == operators, we also need to overwrite .Equals.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) => obj is SyncVarGameObject value && this == value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => Value.GetHashCode();
    }
}
