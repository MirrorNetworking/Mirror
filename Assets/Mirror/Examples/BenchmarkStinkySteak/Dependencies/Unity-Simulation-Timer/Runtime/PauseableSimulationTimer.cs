using UnityEngine;

namespace StinkySteak.SimulationTimer
{
    public struct PauseableSimulationTimer
    {
        public static PauseableSimulationTimer None => default;

        private float _targetTime;
        private bool _isPaused;

        private float _pauseAtTime;

        public float TargetTime => GetTargetTime();
        public bool IsPaused => _isPaused;

        private float GetTargetTime()
        {
            if (!_isPaused)
            {
                return _targetTime;
            }

            return _targetTime + Time.time - _pauseAtTime;
        }

        public static PauseableSimulationTimer CreateFromSeconds(float duration)
        {
            return new PauseableSimulationTimer()
            {
                _targetTime = duration + Time.time
            };
        }

        public void Pause()
        {
            if (_isPaused) return;

            _isPaused = true;
            _pauseAtTime = Time.time;
        }

        public void Resume()
        {
            if (!_isPaused) return;

            _targetTime = GetTargetTime();
            _isPaused = false;
            _pauseAtTime = 0;
        }

        public bool IsRunning => _targetTime > 0;

        public bool IsExpired()
            => Time.time >= TargetTime && IsRunning;

        public bool IsExpiredOrNotRunning()
            => Time.time >= TargetTime;

        public float RemainingSeconds
            => Mathf.Max(TargetTime - Time.time, 0);
    }
}