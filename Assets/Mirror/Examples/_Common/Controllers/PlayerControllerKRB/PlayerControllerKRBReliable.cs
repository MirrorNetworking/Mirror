using UnityEngine;

namespace Mirror.Examples.Common.Controllers.Player
{
    [AddComponentMenu("Network/Player Controller KRB (Reliable)")]
    [RequireComponent(typeof(NetworkTransformReliable))]
    public class PlayerControllerKRBReliable : PlayerControllerKRBBase { }
}
