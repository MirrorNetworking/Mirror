using System;
using UnityEngine;

namespace Mirror
{
    // DEPRECATED 2023-06-15
    [AddComponentMenu("")]
    [Obsolete("NetworkTransform was renamed to NetworkTransformUnreliable.\nYou can easily swap the component's script by going into the Unity Inspector debug mode:\n1. Click the vertical dots on the top right in the Inspector tab.\n2. Find your NetworkTransform component\n3. Drag NetworkTransformUnreliable into the 'Script' field in the Inspector.\n4. Find the three dots and return to Normal mode.")]
    public class NetworkTransform : NetworkTransformUnreliable {}
}
