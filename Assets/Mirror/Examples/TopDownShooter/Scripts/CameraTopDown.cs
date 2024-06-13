using UnityEngine;

namespace Mirror.Examples.TopDownShooter
{
    public class CameraTopDown : MonoBehaviour
    {
        public Transform playerTransform; // Reference to the player's transform
        public Vector3 offset; // Offset from the player

        public float followSpeed = 5f; // Speed at which the camera follows the player

        void LateUpdate()
        {
            if (playerTransform != null)
            {
                Vector3 targetPosition = playerTransform.position + offset;
                transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
            }
        }
    }
}