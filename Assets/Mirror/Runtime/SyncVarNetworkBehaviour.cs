// persistent NetworkBehaviour SyncField which stores netId and component index.
// this is necessary for cases like a player's target.
// the target might run in and out of visibility range and become 'null'.
// but the 'netId' remains and will always point to the monster if around.
// (we also store the component index because GameObject can have multiple
//  NetworkBehaviours of same type)
//
// original Weaver code was broken because it didn't store by netId.
using System;
using System.Runtime.CompilerServices;

namespace Mirror
{
    // SyncField<NetworkBehaviour> needs an uint netId and a byte componentIndex.
    // we use an ulong SyncField internally to store both.
    // while providing .spawned lookup for convenience.
    // NOTE: server always knows all spawned. consider caching the field again.
    // <T> to support abstract NetworkBehaviour and classes inheriting from it.
    //  => hooks can be OnHook(Monster, Monster) instead of OnHook(NB, NB)
    //  => implicit cast can be to/from Monster instead of only NetworkBehaviour
    //  => Weaver needs explicit types for hooks too, not just OnHook(NB, NB)
    public class SyncVarNetworkBehaviour<T> : SyncVar<ulong>
        where T : NetworkBehaviour
    {
        // .spawned lookup from netId overwrites base uint .Value
        public new T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ULongToNetworkBehaviour(base.Value);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => base.Value = NetworkBehaviourToULong(value);
        }

        // OnChanged Callback is for <uint, uint>.
        // Let's also have one for <NetworkBehaviour, NetworkBehaviour>
        public new event Action<T, T> Callback;

        // overwrite CallCallback to use the NetworkIdentity version instead
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void InvokeCallback(ulong oldValue, ulong newValue) =>
            Callback?.Invoke(ULongToNetworkBehaviour(oldValue), ULongToNetworkBehaviour(newValue));

        // ctor
        // 'value = null' so we can do:
        //   SyncVarNetworkBehaviour = new SyncVarNetworkBehaviour()
        // instead of
        //   SyncVarNetworkBehaviour = new SyncVarNetworkBehaviour(null);
        public SyncVarNetworkBehaviour(T value = null)
            : base(NetworkBehaviourToULong(value)) {}

        // implicit conversion: NetworkBehaviour value = SyncFieldNetworkBehaviour
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator T(SyncVarNetworkBehaviour<T> field) => field.Value;

        // implicit conversion: SyncFieldNetworkBehaviour = value
        // even if SyncField is readonly, it's still useful: SyncFieldNetworkBehaviour = target;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator SyncVarNetworkBehaviour<T>(T value) => new SyncVarNetworkBehaviour<T>(value);

        // NOTE: overloading all == operators blocks '== null' checks with an
        // "ambiguous invocation" error. that's good. this way user code like
        // "player.target == null" won't compile instead of silently failing!

        // == operator for comparisons like Player.target==monster
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SyncVarNetworkBehaviour<T> a, SyncVarNetworkBehaviour<T> b) =>
            a.Value == b.Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SyncVarNetworkBehaviour<T> a, SyncVarNetworkBehaviour<T> b) => !(a == b);

        // == operator for comparisons like Player.target==monster
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SyncVarNetworkBehaviour<T> a, NetworkBehaviour b) =>
            a.Value == b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SyncVarNetworkBehaviour<T> a, NetworkBehaviour b) => !(a == b);

        // == operator for comparisons like Player.target==monster
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SyncVarNetworkBehaviour<T> a, T b) =>
            a.Value == b;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SyncVarNetworkBehaviour<T> a, T b) => !(a == b);

        // == operator for comparisons like Player.target==monster
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(NetworkBehaviour a, SyncVarNetworkBehaviour<T> b) =>
            a == b.Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(NetworkBehaviour a, SyncVarNetworkBehaviour<T> b) => !(a == b);

        // == operator for comparisons like Player.target==monster
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(T a, SyncVarNetworkBehaviour<T> b) =>
            a == b.Value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(T a, SyncVarNetworkBehaviour<T> b) => !(a == b);

        // if we overwrite == operators, we also need to overwrite .Equals.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object obj) => obj is SyncVarNetworkBehaviour<T> value && this == value;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => Value.GetHashCode();

        // helper functions to get/set netId, componentIndex from ulong
        // netId on the 4 left bytes. compIndex on the right most byte.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ulong Pack(uint netId, byte componentIndex) =>
            (ulong)netId << 32 | componentIndex;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Unpack(ulong value, out uint netId, out byte componentIndex)
        {
            netId = (uint)(value >> 32);
            componentIndex = (byte)(value & 0xFF);
        }

        // helper function to find/get NetworkBehaviour to ulong (netId/compIndex)
        static T ULongToNetworkBehaviour(ulong value)
        {
            // unpack ulong to netId, componentIndex
            Unpack(value, out uint netId, out byte componentIndex);

            // find spawned NetworkIdentity by netId
            NetworkIdentity identity = Utils.GetSpawnedInServerOrClient(netId);

            // get the nth component
            return identity != null ? (T)identity.NetworkBehaviours[componentIndex] : null;
        }

        static ulong NetworkBehaviourToULong(T value)
        {
            // pack netId, componentIndex to ulong
            return value != null ? Pack(value.netId, (byte)value.ComponentIndex) : 0;
        }

        // Serialize should only write 4+1 bytes, not 8 bytes ulong
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void OnSerializeAll(NetworkWriter writer)
        {
            Unpack(base.Value, out uint netId, out byte componentIndex);
            writer.WriteUInt(netId);
            writer.WriteByte(componentIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void OnSerializeDelta(NetworkWriter writer) =>
            OnSerializeAll(writer);

        // Deserialize should only write 4+1 bytes, not 8 bytes ulong
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void OnDeserializeAll(NetworkReader reader)
        {
            uint netId = reader.ReadUInt();
            byte componentIndex = reader.ReadByte();
            base.Value = Pack(netId, componentIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override void OnDeserializeDelta(NetworkReader reader) =>
            OnDeserializeAll(reader);
    }
}
