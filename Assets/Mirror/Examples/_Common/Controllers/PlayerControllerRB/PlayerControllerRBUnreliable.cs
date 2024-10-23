using UnityEngine;

namespace Mirror.Examples.Common.Controllers.Player
{
    [AddComponentMenu("Network/Player Controller RB (Unreliable)")]
    [RequireComponent(typeof(NetworkTransformUnreliable))]
    public class PlayerControllerRBUnreliable : PlayerControllerRBBase { }
}
