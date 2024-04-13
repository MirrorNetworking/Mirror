// PredictedRigidbody stores a history of its rigidbody states.
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror
{
    // inline everything because this is performance critical!
    public struct RigidbodyState : PredictedState
    {
        public double timestamp { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] private set; }

        // we want to store position delta (last + delta = current), and current.
        // this way we can apply deltas on top of corrected positions to get the corrected final position.
        public Vector3    positionDelta { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] set; } // delta to get from last to this position
        public Vector3    position { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] set; }

        public Quaternion rotationDelta { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] set; } // delta to get from last to this rotation
        public Quaternion rotation { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] set; }

        public Vector3 velocityDelta { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] set; } // delta to get from last to this velocity
        public Vector3 velocity { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] set; }

        public Vector3 angularVelocityDelta { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] set; } // delta to get from last to this velocity
        public Vector3 angularVelocity { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; [MethodImpl(MethodImplOptions.AggressiveInlining)] set; }

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
