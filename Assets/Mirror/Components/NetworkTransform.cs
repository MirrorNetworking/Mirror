using UnityEngine;

namespace Mirror
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/Experimental/NetworkTransformExperimental")]
    [HelpURL("https://mirror-networking.com/docs/Components/NetworkTransform.html")]
    public class NetworkTransform : NetworkTransformBase
    {
        protected override Transform targetTransform => transform;
    }
}
