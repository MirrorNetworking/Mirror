using UnityEngine;

namespace Mirror
{
    // [RequireComponent(typeof(Rigidbody))] <- OnValidate ensures this is on .target
    [AddComponentMenu("Network/Network Rigidbody 2D (Reliable)")]
    public class NetworkRigidbodyReliable2D : NetworkTransformReliable
    {
        bool clientAuthority => syncDirection == SyncDirection.ClientToServer;

        Rigidbody2D rb;
        bool wasKinematic;

        protected override void OnValidate()
        {
            if (Application.isPlaying) return;

            base.OnValidate();

            // we can't overwrite .target to be a Rigidbody.
            // but we can ensure that .target has a Rigidbody, and use it.
            if (target.GetComponent<Rigidbody2D>() == null)
            {
                Debug.LogWarning($"{name}'s NetworkRigidbody2D.target {target.name} is missing a Rigidbody2D", this);
            }
        }

        // cach Rigidbody and original isKinematic setting
        protected override void Awake()
        {
            // we can't overwrite .target to be a Rigidbody.
            // but we can use its Rigidbody component.
            rb = target.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                Debug.LogError($"{name}'s NetworkRigidbody2D.target {target.name} is missing a Rigidbody2D", this);
                return;
            }
#if UNITY_6000_0_OR_NEWER
            wasKinematic = rb.bodyType.HasFlag(RigidbodyType2D.Kinematic);
#else
            wasKinematic = rb.isKinematic;
#endif
            base.Awake();
        }

        // reset forced isKinematic flag to original.
        // otherwise the overwritten value would remain between sessions forever.
        // for example, a game may run as client, set rigidbody.iskinematic=true,
        // then run as server, where .iskinematic isn't touched and remains at
        // the overwritten=true, even though the user set it to false originally.
#if UNITY_6000_0_OR_NEWER
        public override void OnStopServer() => rb.bodyType = wasKinematic ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
        public override void OnStopClient() => rb.bodyType = wasKinematic ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
#else
        public override void OnStopServer() => rb.isKinematic = wasKinematic;
        public override void OnStopClient() => rb.isKinematic = wasKinematic;
#endif

        // overwriting Construct() and Apply() to set Rigidbody.MovePosition
        // would give more jittery movement.

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
#if UNITY_6000_0_OR_NEWER
                if (!owned) rb.bodyType = RigidbodyType2D.Kinematic;
#else
                if (!owned) rb.isKinematic = true;
#endif
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
#if UNITY_6000_0_OR_NEWER
                if (!owned) rb.bodyType = RigidbodyType2D.Kinematic;
#else
                if (!owned) rb.isKinematic = true;
#endif
            }
            // server only
            else if (isServer)
            {
                // on the server, we always own it if clientAuthority is disabled.
                bool owned = !clientAuthority;

                // only set to kinematic if we don't own it
                // otherwise don't touch isKinematic.
                // the authority owner might use it either way.
#if UNITY_6000_0_OR_NEWER
                if (!owned) rb.bodyType = RigidbodyType2D.Kinematic;
#else
                if (!owned) rb.isKinematic = true;
#endif
            }
        }

        protected override void OnTeleport(Vector3 destination)
        {
            base.OnTeleport(destination);

            rb.position = transform.position;
        }

        protected override void OnTeleport(Vector3 destination, Quaternion rotation)
        {
            base.OnTeleport(destination, rotation);

            rb.position = transform.position;
            rb.rotation = transform.rotation.eulerAngles.z;
        }
    }
}
