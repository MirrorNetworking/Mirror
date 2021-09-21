// persistent NetworkIdentity SyncField which stores .netId internally.
// this is necessary for cases like a player's target.
// the target might run in and out of visibility range and become 'null'.
// but the 'netId' remains and will always point to the monster if around.
using System;

namespace Mirror
{
    public class SyncFieldNetworkIdentity : SyncField<NetworkIdentity>
    {
        // ctor
        public SyncFieldNetworkIdentity(NetworkIdentity value, Action<NetworkIdentity, NetworkIdentity> hook = null)
            : base(value, hook) {}

    }
}
