using UnityEngine;
using Mirror;

namespace Mirror.Examples.CouchCoop
{
    public class PlatformMovement : NetworkBehaviour
    {
        // A separate script to handle platform behaviour, see its partner script, MovingPlatform.cs
        private bool onPlatform;
        private Transform platformTransform;
        private Vector3 lastPlatformPosition;

        public override void OnStartAuthority()
        {
            this.enabled = true;
        }

        void FixedUpdate()
        {
            if (onPlatform)
            {
                Vector3 deltaPosition = platformTransform.position - lastPlatformPosition;
                transform.position += deltaPosition;
                lastPlatformPosition = platformTransform.position;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {

            if (collision.gameObject.tag == "Finish")
            {
                platformTransform = collision.gameObject.GetComponent<Transform>();
                lastPlatformPosition = platformTransform.position;
                onPlatform = true;
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            // ideally set a Platform tag, but we'l just use a Unity Pre-set.
            if (collision.gameObject.tag == "Finish")
            {
                onPlatform = false;
                platformTransform = null;
            }
        }
    }
}
