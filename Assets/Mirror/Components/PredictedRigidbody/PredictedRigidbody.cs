// make sure to use a reasonable sync interval.
// for example, correcting every 100ms seems reasonable.
using UnityEngine;

namespace Mirror.PredictedRigidbody
{
    [RequireComponent(typeof(Rigidbody))]
    public class PredictedRigidbody : NetworkBehaviour
    {
        Rigidbody rb;
        Vector3 lastPosition;

        [Tooltip("Broadcast changes if position changed by more than ... meters.")]
        public float positionSensitivity = 0.01f;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        void Update()
        {
            if (Vector3.Distance(transform.position, lastPosition) >= positionSensitivity)
            {
                lastPosition = transform.position;
                SetDirty();
            }
        }

        protected override void OnValidate()
        {
            // make sure syncInterval isn't 0.
            // it's enough to correct every 100ms or so.
            syncInterval = Mathf.Max(syncInterval, 0.1f);
            base.OnValidate();
        }

        // send state to clients every sendInterval.
        // reliable for now.
        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            writer.WriteVector3(rb.position);
            writer.WriteVector3(rb.velocity);
        }

        // read the server's state, but don't apply it directly.
        // this is where reconciliation happens if necessary.
        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            double timestamp = NetworkClient.connection.remoteTimeStamp;
            Vector3 position = reader.ReadVector3();
            Vector3 velocity = reader.ReadVector3();

            // hard force for now.
            // TODO compare past position at timestamp, and only correct if needed
            rb.position = position;
            rb.velocity = velocity;
        }
    }
}
