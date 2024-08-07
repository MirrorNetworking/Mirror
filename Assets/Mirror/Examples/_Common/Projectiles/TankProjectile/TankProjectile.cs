using UnityEngine;

namespace Mirror.Examples.Common
{
    [AddComponentMenu("")]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [DisallowMultipleComponent]
    public class TankProjectile : MonoBehaviour
    {
        [Header("Components")]
        public Rigidbody rigidBody;
        public CapsuleCollider capsuleCollider;

        [Header("Settings")]
        public float destroyAfter = 3f;
        public float force = 1000f;

        enum CapsuleColliderDirection { XAxis, YAxis, ZAxis }

        void OnValidate()
        {
            if (Application.isPlaying) return;
            Reset();
        }

        void Reset()
        {
            rigidBody = GetComponent<Rigidbody>();
            rigidBody.useGravity = false;
            rigidBody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rigidBody.constraints = RigidbodyConstraints.FreezeRotation;

            capsuleCollider = GetComponent<CapsuleCollider>();
            capsuleCollider.direction = (int)CapsuleColliderDirection.ZAxis;
            capsuleCollider.radius = 0.1f;
            capsuleCollider.height = 0.4f;
        }

        // set velocity for server and client. this way we don't have to sync the
        // position, because both the server and the client simulate it.
        void Start()
        {
            rigidBody.AddForce(transform.forward * force);
            Destroy(gameObject, destroyAfter);
        }

        void OnCollisionEnter(Collision collision)
        {
            //Debug.Log($"Hit: {collision.gameObject}");

            if (NetworkServer.active && collision.gameObject.TryGetComponent(out Controllers.Tank.TankHealth tankHealth))
                tankHealth.TakeDamage(1);

            Destroy(gameObject);
        }
    }
}
