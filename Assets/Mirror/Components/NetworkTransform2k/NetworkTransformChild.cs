// A component to synchronize the position of child transforms of networked objects.
// There must be a NetworkTransform on the root object of the hierarchy. There can be multiple NetworkTransformChild components on an object. This does not use physics for synchronization, it simply synchronizes the localPosition and localRotation of the child transform and lerps towards the recieved values.
using UnityEngine;

namespace Mirror
{
    public class NetworkTransformChild : NetworkTransformBase
    {
        [Header("Target")]
        public Transform target;
        protected override Transform targetComponent => target;
    }
}
