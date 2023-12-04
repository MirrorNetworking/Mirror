// A component to synchronize the position of child transforms of networked objects.
// There must be a NetworkTransform on the root object of the hierarchy. There can be multiple NetworkTransformChild components on an object. This does not use physics for synchronization, it simply synchronizes the localPosition and localRotation of the child transform and lerps towards the recieved values.
using System;
using UnityEngine;

namespace Mirror
{
    // Deprecated 2022-10-25
    [AddComponentMenu("")]
    [Obsolete("NetworkTransformChild is not needed anymore. The .target is now exposed in NetworkTransform itself. Note you can open the Inspector in debug view and replace the source script instead of reassigning everything.")]
    public class NetworkTransformChild : NetworkTransform {}
}
