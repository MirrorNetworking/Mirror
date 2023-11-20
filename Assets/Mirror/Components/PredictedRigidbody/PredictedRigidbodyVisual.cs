using UnityEngine;

namespace Mirror
{
    public class PredictedRigidbodyVisual : MonoBehaviour
    {
        [Tooltip("The predicted rigidbody to follow.")]
        public PredictedRigidbody target;
        Rigidbody targetRigidbody;

        [Tooltip("How fast to interpolate to the target position, relative to how far we are away from it.")]
        public float interpolationSpeed = 10;

        void Awake()
        {
            targetRigidbody = target.GetComponent<Rigidbody>();
        }

        // always follow in late update, after update modified positions
        void LateUpdate()
        {
            // if target gets network destroyed for any reason, destroy visual
            if (targetRigidbody == null || target.gameObject == null)
            {
                Destroy(gameObject);
                return;
            }

            // hard follow for now
            // transform.position = targetRigidbody.position;
            // transform.rotation = targetRigidbody.rotation;

            // we know that the rigidbody currently moves at 'velocity' m/s.
            // if we are further away than let's say one unit of velocity aka
            // what we would move in 1s, then teleport.
            float distance = Vector3.Distance(transform.position, targetRigidbody.position);
            if (distance > targetRigidbody.velocity.magnitude)
            {
                transform.position = targetRigidbody.position;
                transform.rotation = targetRigidbody.rotation;
                Debug.Log($"[PredictedRigidbodyVisual] Teleported because distance {distance:F2} > velocity {targetRigidbody.velocity.magnitude:F2}");
                return;
            }

            // smoothly interpolate to the target position.
            // speed relative to how far away we are
            float step = distance * interpolationSpeed;
            transform.position = Vector3.MoveTowards(transform.position, targetRigidbody.position, step * Time.deltaTime);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRigidbody.rotation, step * Time.deltaTime);
        }
    }
}
