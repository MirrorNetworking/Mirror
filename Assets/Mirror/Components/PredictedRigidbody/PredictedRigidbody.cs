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

        [Header("Smoothing")]
        public bool smoothCorrection = true;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        void ApplyState(Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            // Rigidbody .position teleports, while .MovePosition interpolates
            // TODO is this a good idea? what about next capture while it's interpolating?
            if (smoothCorrection)
            {
                rb.MovePosition(position);
                rb.MoveRotation(rotation);
            }
            else
            {
                rb.position = position;
                rb.rotation = rotation;
            }

            rb.velocity = velocity;
        }

        void UpdateServer()
        {
            if (Vector3.Distance(transform.position, lastPosition) >= positionSensitivity)
            {
                lastPosition = transform.position;
                SetDirty();
            }
        }

        void Update()
        {
            if (isServer) UpdateServer();
        }

        // send state to clients every sendInterval.
        // reliable for now.
        public override void OnSerialize(NetworkWriter writer, bool initialState)
        {
            writer.WriteVector3(rb.position);
            writer.WriteQuaternion(rb.rotation);
            writer.WriteVector3(rb.velocity);
        }

        // read the server's state, but don't apply it directly.
        // this is where reconciliation happens if necessary.
        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            double timestamp    = NetworkClient.connection.remoteTimeStamp;
            Vector3 position    = reader.ReadVector3();
            Quaternion rotation = reader.ReadQuaternion();
            Vector3 velocity    = reader.ReadVector3();

            // hard force for now.
            ApplyState(position, rotation, velocity);
        }
    }
}
