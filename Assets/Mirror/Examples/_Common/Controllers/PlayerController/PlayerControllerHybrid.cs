using UnityEngine;

namespace Mirror.Examples.Common.Controllers.Player
{
    [AddComponentMenu("Network/Player Controller (Hybrid)")]
    [RequireComponent(typeof(NetworkTransformHybrid))]
    public class PlayerControllerHybrid : PlayerControllerBase { }
}
