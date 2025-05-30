using UnityEngine;

namespace Mirror.Examples.Common.Controllers.Player
{
    [AddComponentMenu("")]
    [RequireComponent(typeof(NetworkTransformUnreliable))]
    public class PlayerControllerRBUnreliable : PlayerControllerRBBase
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
            GetComponent<NetworkTransformUnreliable>().updateMethod = UpdateMethod.FixedUpdate;
        }
    }
}
