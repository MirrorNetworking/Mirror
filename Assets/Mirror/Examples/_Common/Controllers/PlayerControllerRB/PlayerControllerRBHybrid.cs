using UnityEngine;

namespace Mirror.Examples.Common.Controllers.Player
{
    [AddComponentMenu("")]
    [RequireComponent(typeof(NetworkTransformHybrid))]
    public class PlayerControllerRBHybrid : PlayerControllerRBBase
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
            GetComponent<NetworkTransformHybrid>().updateMethod = UpdateMethod.FixedUpdate;
        }
    }
}
