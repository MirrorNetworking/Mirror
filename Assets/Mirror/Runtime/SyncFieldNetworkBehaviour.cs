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
