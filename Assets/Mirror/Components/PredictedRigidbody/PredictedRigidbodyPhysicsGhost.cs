// Prediction moves out the Rigidbody & Collider into a separate object.
// this component simply points back to the owner component.
// in case Raycasts hit it and need to know the owner, etc.
using UnityEngine;

namespace Mirror
{
    public class PredictedRigidbodyPhysicsGhost : MonoBehaviour
    {
        // this is performance critical, so store target's .Transform instead of
        // PredictedRigidbody, this way we don't need to call the .transform getter.
        [Tooltip("The predicted rigidbody owner.")]
        public Transform target;
    }
}
