using UnityEngine;

namespace Mirror.Examples.Common.Controllers.Player
{
    [AddComponentMenu("Network/Player Controller RB (Reliable)")]
    [RequireComponent(typeof(NetworkTransformReliable))]
    public class PlayerControllerRBReliable : PlayerControllerRBBase { }
}
