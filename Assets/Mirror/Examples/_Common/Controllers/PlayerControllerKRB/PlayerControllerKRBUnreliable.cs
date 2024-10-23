using UnityEngine;

namespace Mirror.Examples.Common.Controllers.Player
{
    [AddComponentMenu("Network/Player Controller KRB (Unreliable)")]
    [RequireComponent(typeof(NetworkTransformUnreliable))]
    public class PlayerControllerKRBUnreliable : PlayerControllerKRBBase { }
}
