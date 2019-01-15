using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/NetworkTransformChild")]
    public class NetworkTransformChild : NetworkTransformBase
    {
        public Transform target;
        protected override Transform targetComponent { get { return target; } }
    }
}
