// this struct exists only for OnDe/Serialize performance.
// instead of WriteVector3+Quaternion+Vector3+Vector3,
// we read & write the whole struct as blittable once.
//
// struct packing can cause odd results with blittable on different platforms,
// so this is usually not recommended!
//
// in this case however, we need to squeeze everything we can out of prediction
// to support low even devices / VR.
using System.Runtime.InteropServices;
using UnityEngine;

namespace Mirror
{
    // struct packing

    [StructLayout(LayoutKind.Sequential)] // explicitly force sequential
    public struct PredictedSyncData
    {
        public float deltaTime;         // 4 bytes (word aligned)
        public Vector3 position;        // 12 bytes (word aligned)
        public Quaternion rotation;     // 16 bytes (word aligned)
        public Vector3 velocity;        // 12 bytes (word aligned)
        public Vector3 angularVelocity; // 12 bytes (word aligned)
        // DO NOT SYNC SLEEPING! this cuts benchmark performance in half(!!!)
        // public byte sleeping;           // 1 byte: bool isn't blittable

        // constructor for convenience
        public PredictedSyncData(float deltaTime, Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)//, bool sleeping)
        {
            this.deltaTime = deltaTime;
            this.position = position;
            this.rotation = rotation;
            this.velocity = velocity;
            this.angularVelocity = angularVelocity;
            // DO NOT SYNC SLEEPING! this cuts benchmark performance in half(!!!)
            // this.sleeping = sleeping ? (byte)1 : (byte)0;
        }
    }

    // NetworkReader/Writer extensions to write this struct
    public static class PredictedSyncDataReadWrite
    {
        public static void WritePredictedSyncData(this NetworkWriter writer, PredictedSyncData data)
        {
            writer.WriteBlittable(data);
        }

        public static PredictedSyncData ReadPredictedSyncData(this NetworkReader reader)
        {
            return reader.ReadBlittable<PredictedSyncData>();
        }
    }
}
