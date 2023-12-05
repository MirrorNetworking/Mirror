using UnityEngine;

namespace StinkySteak.NetcodeBenchmark
{
    [CreateAssetMenu(fileName = nameof(BehaviourConfig), menuName = "Netcode Benchmark/Behaviour Config")]
    public class BehaviourConfig : ScriptableObject
    {
        [SerializeField] private MoveBehaviour _moveBehaviour;

        [System.Serializable]
        public struct MoveBehaviour
        {
            public SinMoveYWrapper SinYMove;
            public SinRandomMoveWrapper SinAllAxisMove;
            public WanderMoveWrapper WanderMove;

            public void CreateDefault()
            {
                SinYMove = SinMoveYWrapper.CreateDefault();
                SinAllAxisMove = SinRandomMoveWrapper.CreateDefault();
                WanderMove = WanderMoveWrapper.CreateDefault();
            }
        }

        private void Reset()
        {
            _moveBehaviour.CreateDefault();
        }

        public void ApplyConfig(ref SinMoveYWrapper wrapper)
        {
            wrapper = _moveBehaviour.SinYMove;
        }

        public void ApplyConfig(ref SinRandomMoveWrapper wrapper)
        {
            wrapper = _moveBehaviour.SinAllAxisMove;
        }

        public void ApplyConfig(ref WanderMoveWrapper wrapper)
        {
            wrapper = _moveBehaviour.WanderMove;
        }
    }
}