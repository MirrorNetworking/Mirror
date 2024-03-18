using UnityEngine;

namespace StinkySteak.NetcodeBenchmark
{
    [System.Serializable]
    public struct WanderMoveWrapper : IMoveWrapper
    {
        [SerializeField] private float _circleRadius;
        [SerializeField] private float _turnChance;
        [SerializeField] private float _maxRadius;

        [SerializeField] private float _mass;
        [SerializeField] private float _maxSpeed;
        [SerializeField] private float _maxForce;

        [SerializeField] private float _maxSpawnPositionRadius;

        private Vector3 _velocity;
        private Vector3 _wanderForce;
        private Vector3 _target;

        public static WanderMoveWrapper CreateDefault()
        {
            WanderMoveWrapper data = new WanderMoveWrapper();
            data._circleRadius = 1;
            data._turnChance = 0.05f;
            data._maxRadius = 5;
            data._mass = 15;
            data._maxSpeed = 3;
            data._maxForce = 15;

            return data;
        }

        public void NetworkStart(Transform transform)
        {
            _velocity = Random.onUnitSphere;
            _wanderForce = GetRandomWanderForce();
            transform.position = RandomVector3.Get(_maxSpawnPositionRadius);
        }

        public void NetworkUpdate(Transform transform)
        {
            var desiredVelocity = GetWanderForce(transform);
            desiredVelocity = desiredVelocity.normalized * _maxSpeed;

            var steeringForce = desiredVelocity - _velocity;
            steeringForce = Vector3.ClampMagnitude(steeringForce, _maxForce);
            steeringForce /= _mass;

            _velocity = Vector3.ClampMagnitude(_velocity + steeringForce, _maxSpeed);
            transform.position += _velocity * Time.deltaTime;
            transform.forward = _velocity.normalized;

            Debug.DrawRay(transform.position, _velocity.normalized * 2, Color.green);
            Debug.DrawRay(transform.position, desiredVelocity.normalized * 2, Color.magenta);
        }

        private Vector3 GetWanderForce(Transform transform)
        {
            if (transform.position.magnitude > _maxRadius)
            {
                var directionToCenter = (_target - transform.position).normalized;
                _wanderForce = _velocity.normalized + directionToCenter;
            }
            else if (Random.value < _turnChance)
            {
                _wanderForce = GetRandomWanderForce();
            }

            return _wanderForce;
        }

        private Vector3 GetRandomWanderForce()
        {
            var circleCenter = _velocity.normalized;
            var randomPoint = Random.insideUnitCircle;

            var displacement = new Vector3(randomPoint.x, randomPoint.y) * _circleRadius;
            displacement = Quaternion.LookRotation(_velocity) * displacement;

            var wanderForce = circleCenter + displacement;
            return wanderForce;
        }
    }
}