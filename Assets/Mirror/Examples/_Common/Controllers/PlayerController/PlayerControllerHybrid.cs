using UnityEngine;

namespace Mirror.Examples.Common.Controllers.Player
{
    [AddComponentMenu("Network/Player Controller (Hybrid)")]
    [RequireComponent(typeof(NetworkTransformUnreliableCompressed))]
    public class PlayerControllerHybrid : PlayerControllerBase { }
}
