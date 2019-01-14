using UnityEngine;

namespace Mirror
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkTransform")]
    public class NetworkTransform : NetworkTransformBase
    {
        protected override Transform targetComponent { get { return transform; } }
    }
}