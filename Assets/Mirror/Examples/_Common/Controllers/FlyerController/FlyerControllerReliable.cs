using UnityEngine;

namespace Mirror.Examples.Common.Controllers.Flyer
{
    [AddComponentMenu("Network/Flyer Controller (Reliable)")]
    [RequireComponent(typeof(NetworkTransformReliable))]
    public class FlyerControllerReliable : FlyerControllerBase { }
}
