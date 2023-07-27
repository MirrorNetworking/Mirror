// Applies HistoryBounds to the physics world by projecting to a trigger Collider.
// This way we can use Physics.Raycast on it.
using System;
using UnityEngine;

namespace Mirror
{
    [RequireComponent(typeof(BoxCollider))]
    public class HistoryCollider : MonoBehaviour
    {
        [Header("Components")]
        [Tooltip("The object's actual collider. We need to know where it is, and how large it is.")]
        public Collider actualCollider;

        [Tooltip("The helper collider that the history bounds are projected onto.\nNeeds to be added for this component, and only used by this component.")]
        public BoxCollider boundsCollider;

        [Header("History")]
        [Tooltip("Keep this many past bounds in the buffer. The larger this is, the further we can raycast into the past.\nMaximum time := historyAmount * captureInterval")]
        public int boundsLimit = 8;

        [Tooltip("Gather N bounds at a time into a bucket for faster encapsulation. A factor of 2 will be twice as fast, etc.")]
        public int boundsPerBucket = 2;

        [Tooltip("Capture bounds every 'captureInterval' seconds. Larger values will require fewer computations, but may not capture every small move.")]
        public float captureInterval = 0.100f; // 100 ms
        double lastCaptureTime = 0;

        [Header("Debug")]
        public Color historyColor = new Color(1.0f, 0.5f, 0.0f, 1.0f);
        public Color currentColor = Color.red;

        protected HistoryBounds history = null;

        protected virtual void Awake()
        {
            history = new HistoryBounds(boundsLimit, boundsPerBucket);

            // ensure colliders were set.
            // bounds collider should always be a trigger.
            if (actualCollider == null)    Debug.LogError("HistoryCollider: actualCollider was not set.");
            if (boundsCollider == null)    Debug.LogError("HistoryCollider: boundsCollider was not set.");
            if (!boundsCollider.isTrigger) Debug.LogError("HistoryCollider: boundsCollider must be a trigger.");
        }

        // capturing and projecting onto colliders should use physics update
        protected virtual void FixedUpdate()
        {
            // capture current bounds every interval
            if (NetworkTime.localTime >= lastCaptureTime + captureInterval)
            {
                lastCaptureTime = NetworkTime.localTime;
                CaptureBounds();
            }

            // project bounds onto helper collider
            ProjectBounds();
        }

        protected virtual void CaptureBounds()
        {
            // grab current collider bounds
            // this is in world space coordinates, and axis aligned
            // TODO double check
            Bounds bounds = actualCollider.bounds;

            // insert into history
            history.Insert(bounds);
        }

        protected virtual void ProjectBounds()
        {
            // grab total collider encapsulating all of history
            Bounds total = history.total;

            // TODO project onto helper collider, but rotate AABB
        }

        // TODO runtime drawing for debugging?
        protected virtual void OnDrawGizmos()
        {
            // draw total bounds
            Gizmos.color = historyColor;
            Gizmos.DrawWireCube(history.total.center, history.total.size);

            // draw current bounds
            Gizmos.color = currentColor;
            Gizmos.DrawWireCube(actualCollider.bounds.center, actualCollider.bounds.size);
        }
    }
}
