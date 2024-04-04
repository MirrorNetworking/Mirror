// PredictedRigidbody stores a history of its rigidbody states.
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Mirror
{
    // inline everything because this is performance critical!
    public struct ForecastRigidbodyState
    {
        public double timestamp;

        // we want to store position delta (last + delta = current), and current.
        // this way we can apply deltas on top of corrected positions to get the corrected final position.
        public Vector3    position;
        public Quaternion rotation;

        public ForecastRigidbodyState(
            double timestamp,
            Vector3 position,
            Quaternion rotation)
        {
            this.timestamp     = timestamp;
            this.position      = position;
            this.rotation      = rotation;
        }

        public static ForecastRigidbodyState Interpolate(ForecastRigidbodyState a, ForecastRigidbodyState b, float t)
        {
            return new ForecastRigidbodyState
            {
                position = Vector3.Lerp(a.position, b.position, t),
                // Quaternions always need to be normalized in order to be a valid rotation after operations
                rotation = Quaternion.Slerp(a.rotation, b.rotation, t).normalized,
            };
        }
    }
}
