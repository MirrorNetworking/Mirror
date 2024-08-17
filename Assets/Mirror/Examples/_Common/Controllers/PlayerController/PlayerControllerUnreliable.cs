using UnityEngine;

namespace Mirror.Examples.Common.Controllers.Player
{
    [AddComponentMenu("Network/Player Controller (Unreliable)")]
    [RequireComponent(typeof(NetworkTransformUnreliable))]
    public class PlayerControllerUnreliable : PlayerControllerBase { }
}
