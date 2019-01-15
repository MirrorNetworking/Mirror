using UnityEngine;
using Mirror;

public class Move : NetworkBehaviour
{
    public CharacterController controller;
    public float speed = 5;
    public float rotationSpeed = 6;

    void Update()
    {
        // movement for local player
        if (!isLocalPlayer) return;

        // rotate
        transform.Rotate(0, Input.GetAxis("Horizontal") * rotationSpeed, 0);

        // move
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        controller.SimpleMove(forward * Input.GetAxis("Vertical") * speed);
    }
}
