using UnityEngine;

namespace StinkySteak.SimulationTimer
{
    public struct SimulationTimer
    {
        public static SimulationTimer None => default;

        private float _targetTime;

        public float TargetTime => _targetTime;

        public static SimulationTimer CreateFromSeconds(float duration)
        {
            return new SimulationTimer()
            {
                _targetTime = duration + Time.time
            };
        }

        public bool IsRunning => _targetTime > 0;

        public bool IsExpired()
            => Time.time >= _targetTime && IsRunning;

        public bool IsExpiredOrNotRunning()
            => Time.time >= _targetTime;

        public float RemainingSeconds
            => Mathf.Max(_targetTime - Time.time, 0);
    }
}
