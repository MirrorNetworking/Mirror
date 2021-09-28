// persistent NetworkBehaviour SyncField which stores netId and component index.
// this is necessary for cases like a player's target.
// the target might run in and out of visibility range and become 'null'.
// but the 'netId' remains and will always point to the monster if around.
// (we also store the component index because GameObject can have multiple
//  NetworkBehaviours of same type)
//
// original Weaver code was broken because it didn't store by netId.
using System;

namespace Mirror
{
    // SyncField<NetworkBehaviour> needs an uint netId and a byte componentIndex.
    // we use an ulong SyncField internally to store both.
    // while providing .spawned lookup for convenience.
    // NOTE: server always knows all spawned. consider caching the field again.
    public class SyncVarNetworkBehaviour : SyncVar<ulong>
    {
        // .spawned lookup from netId overwrites base uint .Value
        public new NetworkBehaviour Value
        {
            get => ULongToNetworkBehaviour(base.Value);
            set => base.Value = NetworkBehaviourToULong(value);
        }

        // ctor
        // 'value = null' so we can do:
        //   SyncVarNetworkBehaviour = new SyncVarNetworkBehaviour()
        // instead of
        //   SyncVarNetworkBehaviour = new SyncVarNetworkBehaviour(null);
        public SyncVarNetworkBehaviour(NetworkBehaviour value = null, Action<NetworkBehaviour, NetworkBehaviour> hook = null)
            : base(NetworkBehaviourToULong(value),
                   hook != null ? WrapHook(hook) : null) {}

        // implicit conversion: NetworkBehaviour value = SyncFieldNetworkBehaviour
        public static implicit operator NetworkBehaviour(SyncVarNetworkBehaviour field) => field.Value;

        // implicit conversion: SyncFieldNetworkBehaviour = value
        // even if SyncField is readonly, it's still useful: SyncFieldNetworkBehaviour = target;
        public static implicit operator SyncVarNetworkBehaviour(NetworkBehaviour value) => new SyncVarNetworkBehaviour(value);

        // wrap <NetworkIdentity> hook within base <uint> hook
        static Action<ulong, ulong> WrapHook(Action<NetworkBehaviour, NetworkBehaviour> hook) =>
            (oldValue, newValue) => { hook(ULongToNetworkBehaviour(oldValue), ULongToNetworkBehaviour(newValue)); };

        // helper functions to get/set netId, componentIndex from ulong
        internal static ulong Pack(uint netId, byte componentIndex)
        {
            // netId on the 4 left bytes. compIndex on the right most byte.
            return (ulong)netId << 32 | componentIndex;
        }

        internal static void Unpack(ulong value, out uint netId, out byte componentIndex)
        {
            netId = (uint)(value >> 32);
            componentIndex = (byte)(value & 0xFF);
        }

        // helper function to find/get NetworkBehaviour to ulong (netId/compIndex)
        static NetworkBehaviour ULongToNetworkBehaviour(ulong value)
        {
            // unpack ulong to netId, componentIndex
            Unpack(value, out uint netId, out byte componentIndex);

            // find spawned NetworkIdentity by netId
            NetworkIdentity identity = Utils.GetSpawnedInServerOrClient(netId);

            // get the nth component
            return identity != null ? identity.NetworkBehaviours[componentIndex] : null;
        }

        static ulong NetworkBehaviourToULong(NetworkBehaviour value)
        {
            // pack netId, componentIndex to ulong
            return value != null ? Pack(value.netId, (byte)value.ComponentIndex) : 0;
        }

        // Serialize should only write 4+1 bytes, not 8 bytes ulong
        public override void OnSerializeAll(NetworkWriter writer)
        {
            Unpack(base.Value, out uint netId, out byte componentIndex);
            writer.WriteUInt(netId);
            writer.WriteByte(componentIndex);
        }

        public override void OnSerializeDelta(NetworkWriter writer) =>
            OnSerializeAll(writer);

        // Deserialize should only write 4+1 bytes, not 8 bytes ulong
        public override void OnDeserializeAll(NetworkReader reader)
        {
            uint netId = reader.ReadUInt();
            byte componentIndex = reader.ReadByte();
            base.Value = Pack(netId, componentIndex);
        }

        public override void OnDeserializeDelta(NetworkReader reader) =>
            OnDeserializeAll(reader);
    }
}
