// sync a GameObject's name over the network.
// useful for debugging (same name on server and client),
// loading configurations per-character, healthbars, etc.
using UnityEngine;

namespace Mirror
{
    [DisallowMultipleComponent]
    public class NetworkName : NetworkBehaviour
    {
        // server-side serialization
        public override void OnSerialize(NetworkWriter writer, bool initialState) =>
            writer.WriteString(name);

        // client-side deserialization
        public override void OnDeserialize(NetworkReader reader, bool initialState) =>
            name = reader.ReadString();
    }
}
