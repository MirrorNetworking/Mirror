// persistent NetworkIdentity SyncField which stores .netId internally.
// this is necessary for cases like a player's target.
// the target might run in and out of visibility range and become 'null'.
// but the 'netId' remains and will always point to the monster if around.
using System;

namespace Mirror
{
    public class SyncFieldNetworkIdentity : SyncField<NetworkIdentity>
    {
        // persistent netId field of the Value's NetworkIdentity
        uint netId;

        // overwrite .Value to get/set NetworkIdentity with our stored netId
        public override NetworkIdentity Value
        {
            get
            {
                // server: use field directly. server knows all NetworkIdentities
                if (NetworkServer.active)
                {
                    return base.Value;
                }

                // client: look up in spawned by netId
                if (NetworkClient.active)
                {
                    NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity entry);
                    return entry;
                }

                return null;
            }
            set
            {
                throw new NotImplementedException();
                /* ORIGINAL WEAVER:
                if (!SyncVarNetworkIdentityEqual(value, netId))
                {
                    NetworkIdentity networktarget = Networktarget;
                    SetSyncVarNetworkIdentity(value, ref target, 1uL, ref netId);
                }
                */
            }
        }

        // ctor
        public SyncFieldNetworkIdentity(NetworkIdentity value, Action<NetworkIdentity, NetworkIdentity> hook = null)
            : base(value, hook) {}
    }
}
