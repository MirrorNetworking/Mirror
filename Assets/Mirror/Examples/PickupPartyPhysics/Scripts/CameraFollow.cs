using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform targetTransform;
    public Vector3 offset;
    public float followSpeed = 5f;
    public float rotationSpeed = 5f;
    public bool followZX = false;

#if !UNITY_SERVER
    void LateUpdate()
    {
        if (targetTransform != null)
        {
            Vector3 targetPosition = targetTransform.position + offset;
            if (followZX == false)
            {
                // set to cached origin if 0,0 isnt always going to be correct
                targetPosition.x = 0;
                targetPosition.z = 0;
            }
            transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);

            Vector3 directionToTarget = (targetTransform.position - transform.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        }
    }
#endif
}