using UnityEngine;

namespace Mirror
{
    /// <summary>
    /// A component to synchronize the position of child transforms of networked objects.
    /// <para>There must be a NetworkTransform on the root object of the hierarchy. There can be multiple NetworkTransformChild components on an object. This does not use physics for synchronization, it simply synchronizes the localPosition and localRotation of the child transform and lerps towards the recieved values.</para>
    /// </summary>
    [AddComponentMenu("Network/NetworkTransformChild")]
    [HelpURL("https://vis2k.github.io/Mirror/Components/NetworkTransformChild")]
    public class NetworkTransformChild : NetworkTransformBase
    {
        public Transform target;
        protected override Transform targetComponent => target;
    }
}
