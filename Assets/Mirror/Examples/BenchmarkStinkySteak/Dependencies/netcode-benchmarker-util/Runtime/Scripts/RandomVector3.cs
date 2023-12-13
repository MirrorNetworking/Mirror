using UnityEngine;

namespace StinkySteak.NetcodeBenchmark
{
    public static class RandomVector3
    {
        public static Vector3 Get(float max)
        {
            float x = Random.Range(-max, max);
            float y = Random.Range(-max, max);
            float z = Random.Range(-max, max);

            return new Vector3(x, y, z);
        }
    }
}