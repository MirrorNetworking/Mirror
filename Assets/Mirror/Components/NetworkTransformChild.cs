using UnityEngine;

namespace Mirror
{
    [AddComponentMenu("Network/NetworkTransformChild")]
    [HelpURL("https://vis2k.github.io/Mirror/Components/NetworkTransformChild")]
    public class NetworkTransformChild : NetworkTransformBase
    {
        public Transform target;
        protected override Transform targetComponent => target;
    }
}
