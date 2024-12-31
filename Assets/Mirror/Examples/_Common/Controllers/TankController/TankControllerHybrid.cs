using UnityEngine;

namespace Mirror.Examples.Common.Controllers.Tank
{
    [AddComponentMenu("Network/Tank Controller (Hybrid)")]
    [RequireComponent(typeof(NetworkTransformHybrid))]
    public class TankControllerHybrid : TankControllerBase { } 
}
