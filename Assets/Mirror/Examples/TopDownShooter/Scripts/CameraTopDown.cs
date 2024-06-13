using UnityEngine;

namespace Mirror.Examples.TopDownShooter
{
    public class CameraTopDown : MonoBehaviour
    {
        public Transform playerTransform;
        public Vector3 offset;
        public float followSpeed = 5f;

#if !UNITY_SERVER
        void LateUpdate()
        {
            if (playerTransform != null)
            {
                Vector3 targetPosition = playerTransform.position + offset;
                transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
            }
        }
#endif
    }
}