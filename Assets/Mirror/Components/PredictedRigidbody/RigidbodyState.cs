// PredictedRigidbody stores a history of its rigidbody states.
using UnityEngine;

namespace Mirror
{
    public struct RigidbodyState : PredictedState
    {
        public double timestamp { get; private set; }

        // we want to store position delta (last + delta = current), and current.
        // this way we can apply deltas on top of corrected positions to get the corrected final position.
        public Vector3    positionDelta { get; set; } // delta to get from last to this position
        public Vector3    position { get; set; }

        public Quaternion rotationDelta { get; set; } // delta to get from last to this rotation
        public Quaternion rotation { get; set; }

        public Vector3 velocityDelta { get; set; } // delta to get from last to this velocity
        public Vector3 velocity { get; set; }

        public Vector3 angularVelocityDelta { get; set; } // delta to get from last to this velocity
        public Vector3 angularVelocity { get; set; }

        public RigidbodyState(
            double timestamp,
            Vector3 positionDelta,
            Vector3 position,
            Quaternion rotationDelta,
            Quaternion rotation,
            Vector3 velocityDelta,
            Vector3 velocity,
            Vector3 angularVelocityDelta,
            Vector3 angularVelocity)
        {
            this.timestamp     = timestamp;
            this.positionDelta = positionDelta;
            this.position      = position;
            this.rotationDelta = rotationDelta;
            this.rotation      = rotation;
            this.velocityDelta = velocityDelta;
            this.velocity      = velocity;
            this.angularVelocityDelta = angularVelocityDelta;
            this.angularVelocity      = angularVelocity;
        }

        public static RigidbodyState Interpolate(RigidbodyState a, RigidbodyState b, float t)
        {
            return new RigidbodyState
            {
                position = Vector3.Lerp(a.position, b.position, t),
                // Quaternions always need to be normalized in order to be a valid rotation after operations
                rotation = Quaternion.Slerp(a.rotation, b.rotation, t).normalized,
                velocity = Vector3.Lerp(a.velocity, b.velocity, t),
                angularVelocity = Vector3.Lerp(a.angularVelocity, b.angularVelocity, t)
            };
        }
    }
}
