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
using UnityEngine;

namespace Mirror
{
    // SyncField<GameObject> only stores an uint netId.
    // while providing .spawned lookup for convenience.
    // NOTE: server always knows all spawned. consider caching the field again.
    public class SyncFieldGameObject : SyncField<uint>
    {
        // .spawned lookup from netId overwrites base uint .Value
        public new GameObject Value
        {
            get => GetGameObject(base.Value);
            set => base.Value = GetNetId(value);
        }

        // ctor
        public SyncFieldGameObject(GameObject value, Action<GameObject, GameObject> hook = null)
            : base(GetNetId(value),
                   hook != null ? WrapHook(hook) : null) {}

        // helper function to get netId from GameObject (if any)
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
        static GameObject GetGameObject(uint netId)
        {
            NetworkIdentity spawned = Utils.GetSpawnedInServerOrClient(netId);
            return spawned != null ? spawned.gameObject : null;
        }

        // wrap <GameObject> hook within base <uint> hook
        static Action<uint, uint> WrapHook(Action<GameObject, GameObject> hook) =>
            (oldValue, newValue) => {
                hook(GetGameObject(oldValue), GetGameObject(newValue));
            };
    }
}
