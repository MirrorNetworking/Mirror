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

        public Quaternion rotation; // TODO delta rotation?

        public Vector3 velocityDelta { get; set; } // delta to get from last to this velocity
        public Vector3 velocity { get; set; }

        public RigidbodyState(
            double timestamp,
            Vector3 positionDelta, Vector3 position,
            Quaternion rotation,
            Vector3 velocityDelta, Vector3 velocity)
        {
            this.timestamp     = timestamp;
            this.positionDelta = positionDelta;
            this.position      = position;
            this.rotation      = rotation;
            this.velocityDelta = velocityDelta;
            this.velocity      = velocity;
        }

        // adjust the deltas after inserting a correction between this one and the previous one.
        public void AdjustDeltas(float multiplier)
        {
            positionDelta = Vector3.Lerp(Vector3.zero, positionDelta, multiplier);
            // TODO if we have have a rotation delta, then scale it here too
            velocityDelta = Vector3.Lerp(Vector3.zero, velocityDelta, multiplier);
        }

        public static RigidbodyState Interpolate(RigidbodyState a, RigidbodyState b, float t)
        {
            return new RigidbodyState
            {
                position = Vector3.Lerp(a.position, b.position, t),
                rotation = Quaternion.Slerp(a.rotation, b.rotation, t),
                velocity = Vector3.Lerp(a.velocity, b.velocity, t)
            };
        }
    }
}
