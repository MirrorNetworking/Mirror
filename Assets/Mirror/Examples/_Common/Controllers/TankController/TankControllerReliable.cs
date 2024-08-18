using UnityEngine;

namespace Mirror.Examples.Common.Controllers.Tank
{
    [AddComponentMenu("Network/Tank Controller (Reliable)")]
    [RequireComponent(typeof(NetworkTransformReliable))]
    public class TankControllerReliable : TankControllerBase { } 
}
