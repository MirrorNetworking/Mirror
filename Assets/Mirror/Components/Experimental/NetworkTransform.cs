using System;
using UnityEngine;

namespace Mirror.Experimental
{
    [DisallowMultipleComponent]
    // Deprecated 2022-01-18
    [Obsolete("Use the default NetworkTransform instead, it has proper snapshot interpolation.")]
    [AddComponentMenu("")]
    public class NetworkTransform : NetworkTransformBase
    {
        protected override Transform targetTransform => transform;
    }
}
