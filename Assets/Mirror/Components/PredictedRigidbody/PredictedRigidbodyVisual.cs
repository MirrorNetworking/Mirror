using UnityEngine;

namespace Mirror
{
    public class PredictedRigidbodyVisual : MonoBehaviour
    {
        [Tooltip("The predicted rigidbody to follow.")]
        public PredictedRigidbody target;
        Rigidbody targetRigidbody;

        [Tooltip("How fast to interpolate to the target position, relative to how far we are away from it.\nHigher value will be more jitter but sharper moves, lower value will be less jitter but a little too smooth / rounded moves.")]
        public float interpolationSpeed = 15; // 10 is a little too low for billiards at least

        [Tooltip("Teleport if we are further than 'multiplier x collider size' behind.")]
        public float teleportDistanceMultiplier = 10;

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

            // hard follow:
            // transform.position = targetRigidbody.position;
            // transform.rotation = targetRigidbody.rotation;

            // if we are further than N colliders sizes behind, then teleport
            float colliderSize = target.GetComponent<Collider>().bounds.size.magnitude;
            float threshold = colliderSize * teleportDistanceMultiplier;
            float distance = Vector3.Distance(transform.position, targetRigidbody.position);
            if (distance > threshold)
            {
                transform.position = targetRigidbody.position;
                transform.rotation = targetRigidbody.rotation;
                Debug.Log($"[PredictedRigidbodyVisual] Teleported because distance {distance:F2} > threshold {threshold:F2}");
                return;
            }

            // smoothly interpolate to the target position.
            // speed relative to how far away we are
            float step = distance * interpolationSpeed;
            // speed relative to how far away we are.
            // => speed increases by distance² because the further away, the
            //    sooner we need to catch the fuck up
            // float step = (distance * distance) * interpolationSpeed;
            transform.position = Vector3.MoveTowards(transform.position, targetRigidbody.position, step * Time.deltaTime);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRigidbody.rotation, step * Time.deltaTime);
        }
    }
}
