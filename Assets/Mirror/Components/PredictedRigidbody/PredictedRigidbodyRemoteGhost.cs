// simply ghost object that always follows last received server state.
using UnityEngine;

namespace Mirror
{
    public class PredictedRigidbodyRemoteGhost : MonoBehaviour
    {
        // this is performance critical, so store target's .Transform instead of
        // PredictedRigidbody, this way we don't need to call the .transform getter.
        [Tooltip("The predicted rigidbody owner.")]
        public Transform target;

        // ghost (settings are copyed from PredictedRigidbody)
        MeshRenderer ghost;
        public float ghostDistanceThreshold = 0.1f;
        public float ghostEnabledCheckInterval = 0.2f;
        double lastGhostEnabledCheckTime = 0;

        // cache components because this is performance critical!
        Transform tf;

        // we add this component manually from PredictedRigidbody.
        // so assign this in Start. target isn't set in Awake yet.
        void Start()
        {
            tf = transform;
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
            bool insideTarget = Vector3.Distance(tf.position, target.position) <= ghostDistanceThreshold;
            ghost.enabled = !insideTarget;
        }

        void Update() => UpdateGhostRenderers();

        // always follow in late update, after update modified positions
        void LateUpdate()
        {
            // if owner gets network destroyed for any reason, destroy visual
            if (target == null) Destroy(gameObject);
        }
    }
}
