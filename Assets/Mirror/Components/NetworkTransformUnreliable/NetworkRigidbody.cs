using UnityEngine;

namespace Mirror
{
    [RequireComponent(typeof(Rigidbody))]
    public class NetworkRigidbody : NetworkTransform
    {
        // cache the Rigidbody component
        Rigidbody rb;
        void Awake() { rb = GetComponent<Rigidbody>(); }

        // FixedUpdate for physics
        void FixedUpdate()
        {
            // who ever has authority moves the Rigidbody with physics.
            // everyone else simply sets it to kinematic.
            // so that only the Transform component is synced.
            if (isClient)
            {
                // on the client, force kinematic if server has authority.
                // or if another client (not us) has authority.
                // otherwise don't touch isKinematic.
                // the authority owner might use it either way.
                if (!IsClientWithAuthority)
                    rb.isKinematic = true;
            }
            else if (isServer)
            {
                // on the server, force kinematic if a client has authority.
                // otherwise don't touch isKinematic.
                // the authority owner might use it either way.
                if (clientAuthority)
                    rb.isKinematic = true;
            }
        }
    }
}
