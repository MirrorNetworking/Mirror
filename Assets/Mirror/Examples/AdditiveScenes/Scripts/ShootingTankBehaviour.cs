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
            if (IsServer && NetIdentity.observers.Count > 0)
                ShootNearestPlayer();

            if (IsClient)
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, turnSpeed);
        }

        [Server]
        void ShootNearestPlayer()
        {
            GameObject target = null;
            float distance = 100f;

            foreach (INetworkConnection networkConnection in NetIdentity.observers)
            {
                GameObject tempTarget = networkConnection.Identity.gameObject;
                float tempDistance = Vector3.Distance(tempTarget.transform.position, transform.position);

                if (target == null || distance > tempDistance)
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
