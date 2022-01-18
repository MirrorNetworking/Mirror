using System;
using UnityEngine;

namespace Mirror.Experimental
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/Experimental/NetworkTransformExperimental")]
    [HelpURL("https://mirror-networking.gitbook.io/docs/components/network-transform")]
    [Obsolete("Use the default NetworkTransform instead, it has proper snapshot interpolation.")]
    public class NetworkTransform : NetworkTransformBase
    {
        protected override Transform targetTransform => transform;
    }
}
