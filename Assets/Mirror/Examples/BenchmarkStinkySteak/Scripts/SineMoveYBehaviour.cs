using Mirror;
using StinkySteak.NetcodeBenchmark;
using UnityEngine;

namespace StinkySteak.MirrorBenchmark
{
    public class SineMoveYBehaviour : NetworkBehaviour
    {
        [SerializeField] private BehaviourConfig _config;
        private SinMoveYWrapper _wrapper;

        public override void OnStartServer()
        {
            if (isClient) return;

            _config.ApplyConfig(ref _wrapper);
            _wrapper.NetworkStart(transform);
        }

        private void FixedUpdate()
        {
            if (isClient) return;

            _wrapper.NetworkUpdate(transform);
        }
    }
}
