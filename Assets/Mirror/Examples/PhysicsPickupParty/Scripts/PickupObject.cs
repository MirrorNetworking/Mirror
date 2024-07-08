using UnityEngine;
using Mirror;

namespace Mirror.Examples.PhysicsPickupParty
{
    public class PickupObject : MonoBehaviour
    {

        public Rigidbody pickupRigidbody;
        public Collider pickupCollider;
        public NetworkTransformUnreliable networkTransform; // disabling is not always supported, can have weird results

        public GameObject playerHolder; // set per object if its currently picked up
    }
}