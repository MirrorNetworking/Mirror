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
    public struct ForecastSyncData
    {
        public float deltaTime;         // 4 bytes (word aligned)
        public Vector3 position;        // 12 bytes (word aligned)
        public Quaternion rotation;     // 16 bytes (word aligned)

        // constructor for convenience
        public ForecastSyncData(float deltaTime, Vector3 position, Quaternion rotation)//, bool sleeping)
        {
            this.deltaTime = deltaTime;
            this.position = position;
            this.rotation = rotation;
        }
    }

    // NetworkReader/Writer extensions to write this struct
    public static class ForecastSyncDataReadWrite
    {
        public static void WriteForecastSyncData(this NetworkWriter writer, ForecastSyncData data)
        {
            writer.WriteBlittable(data);
        }

        public static ForecastSyncData ReadForecastSyncData(this NetworkReader reader)
        {
            return reader.ReadBlittable<ForecastSyncData>();
        }
    }
}
