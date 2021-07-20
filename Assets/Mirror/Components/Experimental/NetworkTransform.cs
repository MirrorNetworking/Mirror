using System;
using UnityEngine;

namespace Mirror.Experimental
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Network/Experimental/NetworkTransformExperimental")]
    [HelpURL("https://mirror-networking.gitbook.io/docs/components/network-transform")]
    // DEPRECATED 2021-07-20
    [Obsolete("NetworkTransform now uses Snapshot Interpolation. This component will be removed soon.")]
    public class NetworkTransform : NetworkTransformBase
    {
        protected override Transform targetTransform => transform;
    }
}
