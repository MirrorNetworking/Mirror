using UnityEngine;

    public class CameraFollow : MonoBehaviour
    {
        public Transform targetTransform;
        public Vector3 offset;
        public float followSpeed = 5f;
        public GameObject mapCamera;

#if !UNITY_SERVER
        void LateUpdate()
        {
            if (targetTransform != null)
            {
                Vector3 targetPosition = targetTransform.position + offset;
                transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
            }
        }
#endif
    }