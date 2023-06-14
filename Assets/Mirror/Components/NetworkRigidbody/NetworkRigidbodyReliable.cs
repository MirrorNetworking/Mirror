using UnityEngine;

namespace Mirror
{
    [RequireComponent(typeof(Rigidbody))]
    public class NetworkRigidbodyReliable : NetworkTransformReliable
    {
        new bool clientAuthority =>
            syncDirection == SyncDirection.ClientToServer;

        Rigidbody rb;
        bool wasKinematic;

        // cach Rigidbody and original isKinematic setting
        protected override void Awake()
        {
            rb = GetComponent<Rigidbody>();
            wasKinematic = rb.isKinematic;
            base.Awake();
        }

        // reset forced isKinematic flag to original.
        // otherwise the overwritten value would remain between sessions forever.
        // for example, a game may run as client, set rigidbody.iskinematic=true,
        // then run as server, where .iskinematic isn't touched and remains at
        // the overwritten=true, even though the user set it to false originally.
        public override void OnStopServer() => rb.isKinematic = wasKinematic;
        public override void OnStopClient() => rb.isKinematic = wasKinematic;

        // FixedUpdate for physics
        void FixedUpdate()
        {
            // who ever has authority moves the Rigidbody with physics.
            // everyone else simply sets it to kinematic.
            // so that only the Transform component is synced.

            // host mode
            if (isServer && isClient)
            {
                // in host mode, we own it it if:
                // clientAuthority is disabled (hence server / we own it)
                // clientAuthority is enabled and we have authority over this object.
                bool owned = !clientAuthority || IsClientWithAuthority;

                // only set to kinematic if we don't own it
                // otherwise don't touch isKinematic.
                // the authority owner might use it either way.
                if (!owned) rb.isKinematic = true;
            }
            // client only
            else if (isClient)
            {
                // on the client, we own it only if clientAuthority is enabled,
                // and we have authority over this object.
                bool owned = IsClientWithAuthority;

                // only set to kinematic if we don't own it
                // otherwise don't touch isKinematic.
                // the authority owner might use it either way.
                if (!owned) rb.isKinematic = true;
            }
            // server only
            else if (isServer)
            {
                // on the server, we always own it if clientAuthority is disabled.
                bool owned = !clientAuthority;

                // only set to kinematic if we don't own it
                // otherwise don't touch isKinematic.
                // the authority owner might use it either way.
                if (!owned) rb.isKinematic = true;
            }
        }
    }
}
