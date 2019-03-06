using UnityEngine;

namespace Mirror
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkTransform")]
    [HelpURL("https://vis2k.github.io/Mirror/Components/NetworkTransform")]
    public class NetworkTransform : NetworkTransformBase
    {
        protected override Transform targetComponent => transform;
    }
}
