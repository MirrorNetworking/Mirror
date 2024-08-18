using UnityEngine;

namespace Mirror.Examples.Common.Controllers.Tank
{
    [AddComponentMenu("Network/Tank Controller (Unreliable)")]
    [RequireComponent(typeof(NetworkTransformUnreliable))]
    public class TankControllerUnreliable : TankControllerBase { } 
}
