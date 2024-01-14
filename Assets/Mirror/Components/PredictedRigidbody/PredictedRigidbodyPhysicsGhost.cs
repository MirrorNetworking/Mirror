// Prediction moves out the Rigidbody & Collider into a separate object.
// This way the main (visual) object can smoothly follow it, instead of hard.
using System;
using UnityEngine;

namespace Mirror
{
    public class PredictedRigidbodyPhysicsGhost : MonoBehaviour
    {
        [Tooltip("The predicted rigidbody owner.")]
        public PredictedRigidbody target;

        // ghost (settings are copyed from PredictedRigidbody)
        MeshRenderer ghost;
        public float ghostDistanceThreshold = 0.1f;
        public float ghostEnabledCheckInterval = 0.2f;
        double lastGhostEnabledCheckTime = 0;

        // cache
        Collider co;

        // we add this component manually from PredictedRigidbody.
        // so assign this in Start. target isn't set in Awake yet.
        void Start()
        {
            co = GetComponent<Collider>();
            ghost = GetComponent<MeshRenderer>();
        }

        void UpdateGhostRenderers()
        {
            // only if a ghost renderer was given
            if (ghost == null) return;

            // enough to run this in a certain interval.
            // doing this every update would be overkill.
            // this is only for debug purposes anyway.
            if (NetworkTime.localTime < lastGhostEnabledCheckTime + ghostEnabledCheckInterval) return;
            lastGhostEnabledCheckTime = NetworkTime.localTime;

            // only show ghost while interpolating towards the object.
            // if we are 'inside' the object then don't show ghost.
            // otherwise it just looks like z-fighting the whole time.
            // => iterated the renderers we found when creating the visual copy.
            //    we don't want to GetComponentsInChildren every time here!
            bool insideTarget = Vector3.Distance(transform.position, target.transform.position) <= ghostDistanceThreshold;
            ghost.enabled = !insideTarget;
        }

        void Update() => UpdateGhostRenderers();

        // always follow in late update, after update modified positions
        void LateUpdate()
        {
            // if owner gets network destroyed for any reason, destroy visual
            if (target == null || target.gameObject == null) Destroy(gameObject);
        }

        // also show a yellow gizmo for the predicted & corrected physics.
        // in case we can't renderer ghosts, at least we have this.
        void OnDrawGizmos()
        {
            if (co != null)
            {
                // show the client's predicted & corrected physics in yellow
                Bounds bounds = co.bounds;
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }
    }
}
