// script to handle the table's pocket collisions for resets / destruction.
// predicted objects sometimes have their rigidbodies moved out of them.
// which is why we handle collisions in the table itself, not per-object.
// because here we can check who the rigidbody belongs to more easily.
// ... that's just the best practice at the moment, maybe we can make this
//     easier in the future ...
using UnityEngine;

namespace Mirror.Examples.BilliardsPredicted
{
    public class Pockets : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            if (!NetworkServer.active) return;

            // the collider may be on a predicted object or on its ghost object.
            // find the source first.
            if (PredictedRigidbody.IsPredicted(other, out PredictedRigidbody predicted))
            {
                // is it a white ball?
                if (predicted.TryGetComponent(out WhiteBallPredicted white))
                {
                    Rigidbody rigidBody = predicted.predictedRigidbody;
                    rigidBody.position = white.startPosition;
#if UNITY_6000_0_OR_NEWER
                    rigidBody.linearVelocity = Vector3.zero;
#else
                    rigidBody.velocity = Vector3.zero;
#endif
                }

                // is it a read ball?
                if (predicted.GetComponent<RedBallPredicted>())
                {
                    // destroy when entering a pocket.
                    NetworkServer.Destroy(predicted.gameObject);
                }
            }
        }
    }
}
