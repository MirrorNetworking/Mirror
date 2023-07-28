// Applies HistoryBounds to the physics world by projecting to a trigger Collider.
// This way we can use Physics.Raycast on it.
using UnityEngine;

namespace Mirror
{
    public class HistoryCollider : MonoBehaviour
    {
        [Header("Components")]
        [Tooltip("The object's actual collider. We need to know where it is, and how large it is.")]
        public Collider actualCollider;

        [Tooltip("The helper collider that the history bounds are projected onto.\nNeeds to be added to a child GameObject to counter-rotate an axis aligned Bounding Box onto it.\nThis is only used by this component.")]
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
            if (boundsCollider.transform.parent != transform) Debug.LogError("HistoryCollider: boundsCollider must be a child of this GameObject.");
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

            // don't assign empty bounds, this will throw a Unity warning
            if (history.boundsCount == 0) return;

            // scale projection doesn't work yet.
            // for now, don't allow scale changes.
            if (transform.lossyScale != Vector3.one)
            {
                Debug.LogWarning($"HistoryCollider: {name}'s transform global scale must be (1,1,1).");
                return;
            }

            // counter rotate the child collider against the gameobject's rotation.
            // we need this to always be axis aligned.
            boundsCollider.transform.localRotation = Quaternion.Inverse(transform.rotation);

            // project world space bounds to collider's local space
            boundsCollider.center = boundsCollider.transform.InverseTransformPoint(total.center);
            boundsCollider.size   = total.size; // TODO projection?
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
