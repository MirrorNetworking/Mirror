// Applies HistoryBounds to the physics world by projecting to a trigger Collider.
// This way we can use Physics.Raycast on it.
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
            // grab current bounds (collider.bounds returns world coordinates)
            Bounds bounds = actualCollider.bounds;

            // TODO convert to axis aligned world bounding box

            // insert into history
            history.Insert(bounds);
        }

        protected virtual void ProjectBounds()
        {

        }
    }
}
