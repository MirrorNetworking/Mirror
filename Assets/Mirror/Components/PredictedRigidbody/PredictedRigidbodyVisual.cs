using System;
using UnityEngine;

namespace Mirror
{
    [Obsolete("Prediction is under development, do not use this yet.")]
    public class PredictedRigidbodyVisual : MonoBehaviour
    {
        [Tooltip("The predicted rigidbody to follow.")]
        public PredictedRigidbody target;
        Rigidbody targetRigidbody;

        // settings are applied in the other PredictedRigidbody component and then copied here.
        [HideInInspector] public float positionInterpolationSpeed = 15; // 10 is a little too low for billiards at least
        [HideInInspector] public float rotationInterpolationSpeed = 10;
        [HideInInspector] public float teleportDistanceMultiplier = 10;

        // we add this component manually from PredictedRigidbody.
        // so assign this in Start. target isn't set in Awake yet.
        void Start()
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
            float positionStep = distance * positionInterpolationSpeed;
            // speed relative to how far away we are.
            // => speed increases by distance² because the further away, the
            //    sooner we need to catch the fuck up
            // float positionStep = (distance * distance) * interpolationSpeed;
            transform.position = Vector3.MoveTowards(transform.position, targetRigidbody.position, positionStep * Time.deltaTime);

            // smoothly interpolate to the target rotation.
            // Quaternion.RotateTowards doesn't seem to work at all, so let's use SLerp.
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRigidbody.rotation, rotationInterpolationSpeed * Time.deltaTime);
        }
    }
}
