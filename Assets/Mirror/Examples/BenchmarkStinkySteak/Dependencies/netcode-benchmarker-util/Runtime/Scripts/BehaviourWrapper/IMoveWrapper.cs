using UnityEngine;

namespace StinkySteak.NetcodeBenchmark
{
    public interface IMoveWrapper
    {
        void NetworkStart(Transform transform);
        void NetworkUpdate(Transform transform);
    }
}