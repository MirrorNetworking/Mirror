using UnityEngine;

namespace Mirror.Examples.Common.Controllers.Player
{
    [AddComponentMenu("Network/Player Controller RB (Reliable)")]
    [RequireComponent(typeof(NetworkTransformReliable))]
    public class PlayerControllerRBReliable : PlayerControllerRBBase
    {
        protected override void OnValidate()
        {
            if (Application.isPlaying) return;
            base.OnValidate();

            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            GetComponent<NetworkTransformReliable>().useFixedUpdate = true;
        }
    }
}
