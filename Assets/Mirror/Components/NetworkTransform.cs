using UnityEngine;

namespace Mirror
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/NetworkTransform")]
    [HelpURL("https://mirror-networking.com/xmldocs/Components/NetworkTransform.html")]
    public class NetworkTransform : NetworkTransformBase
    {
        protected override Transform targetComponent => transform;
    }
}
