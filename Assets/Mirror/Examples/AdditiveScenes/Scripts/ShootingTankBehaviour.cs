using UnityEngine;

namespace Mirror.Examples.Additive
{
    // This script demonstrates the NetworkAnimator and how to leverage
    // the built-in observers system to track players.
    // Note that all ProximityCheckers should be restricted to the Player layer.
    public class ShootingTankBehaviour : NetworkBehaviour
    {
        [SyncVar]
        public Quaternion rotation;

        NetworkAnimator networkAnimator;

        [ServerCallback]
        void Start()
        {
            networkAnimator = GetComponent<NetworkAnimator>();
        }

        [Range(0, 1)]
        public float turnSpeed = 0.1f;

        void Update()
        {
            if (isServer && netIdentity.observers.Count > 0)
                ShootNearestPlayer();

            if (isClient)
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, turnSpeed);
        }

        [Server]
        void ShootNearestPlayer()
        {
            GameObject target = null;

            // squared distance of 100f for SqrMagnitude comparison
            float distance = 10000f;

            foreach (NetworkConnection networkConnection in netIdentity.observers.Values)
            {
                GameObject tempTarget = networkConnection.identity.gameObject;
                float tempDistance = Vector3.SqrMagnitude(tempTarget.transform.position - transform.position);

                // distance is already squared, don't square again here
                if (target == null || tempDistance < distance)
                {
                    target = tempTarget;
                    distance = tempDistance;
                }
            }

            if (target != null)
            {
                transform.LookAt(target.transform.position + Vector3.down);
                rotation = transform.rotation;
                networkAnimator.SetTrigger("Fire");
            }
        }
    }
}
