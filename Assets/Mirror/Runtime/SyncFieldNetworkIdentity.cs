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
                // (faster than a spawned dictionary lookup)
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
            : base(value, hook)
        {
            // store the NetworkIdentity's netId
            if (value != null)
                netId = value.netId;
        }
    }
}
