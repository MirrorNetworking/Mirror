using UnityEngine;

public class JointForce : MonoBehaviour
{
    public CharacterController characterController;
    public Rigidbody jointRigidbody;
    public float forceMultiplier = 100f;

    private Vector3 lastPosition;

#if !UNITY_SERVER
    void Start()
    {
        lastPosition = characterController.transform.position;
    }


    void FixedUpdate()
    {
        Vector3 movementDelta = characterController.transform.position - lastPosition;
        Vector3 force = -movementDelta * forceMultiplier;
        jointRigidbody.AddForce(force);
        lastPosition = characterController.transform.position;
    }
#endif
}