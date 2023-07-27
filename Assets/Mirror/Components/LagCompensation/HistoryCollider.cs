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
        }

        protected virtual void Update()
        {
            // capture current bounds every interval
            if (NetworkTime.localTime >= lastCaptureTime + captureInterval)
            {
                lastCaptureTime = NetworkTime.localTime;
                Capture();
            }
        }

        protected virtual void Capture()
        {
            // grab current bounds (collider.bounds returns world coordinates)
            Bounds bounds = actualCollider.bounds;

            // TODO convert to axis aligned world bounding box

            // TODO insert into history
        }
    }
}
