using UnityEngine;

namespace Mirror.PredictedRigidbody
{
    [RequireComponent(typeof(Rigidbody))]
    public class PredictedRigidbody : NetworkBehaviour
    {
        Rigidbody rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
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
            double timestamp = connectionToServer.remoteTimeStamp;
            Vector3 position = reader.ReadVector3();
            Vector3 velocity = reader.ReadVector3();
            Debug.Log($"OnDeserialize: {timestamp} {position} {velocity}");
        }
    }
}
