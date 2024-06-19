using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class PlayerPickupParty : NetworkBehaviour
{

    public float moveSpeed = 8f;
    public float armRotationSpeed = 25f;
    public bool canPickup = false;
    public Transform pickedUpObject;
    public Rigidbody pickedUpObjectRigidbody; // cache
    public PlayerArmCollision playerArmCollision;

    public CharacterController characterController;
    public Transform armPivotR, armPivotL;
    private Camera mainCamera;
    

#if !UNITY_SERVER
    public override void OnStartLocalPlayer()
    {
        // Grab and setup camera for local player only
        mainCamera = Camera.main;
    }
#endif

#if !UNITY_SERVER
    [ClientCallback]
    void Update()
    {
        if (!Application.isFocused) return;
        if (isOwned == false) { return; }

        // Handle movement
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");

        Vector3 movement = new Vector3(moveHorizontal, 0f, moveVertical);
        if (movement.magnitude > 1f) movement.Normalize();  // Normalize to prevent faster diagonal movement
        characterController.Move(movement * moveSpeed * Time.deltaTime);

        RotatePlayerToMouse();
        RotateArmToMouse();

        if (Input.GetMouseButtonDown(0))
        {
            Pickup();
        }
    }
#endif

#if !UNITY_SERVER
    [ClientCallback]
    void RotatePlayerToMouse()
    {
        Plane playerPlane = new Plane(Vector3.up, transform.position);
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (playerPlane.Raycast(ray, out float hitDist))
        {
            Vector3 targetPoint = ray.GetPoint(hitDist);
            Quaternion targetRotation = Quaternion.LookRotation(targetPoint - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, moveSpeed * Time.deltaTime);
        }
    }
#endif

#if !UNITY_SERVER
    [ClientCallback]
    void RotateArmToMouse()
    {
        // Get the mouse position in screen space
        Vector3 mouseScreenPosition = Input.mousePosition;

        // Convert the mouse position to world space
        Ray ray = mainCamera.ScreenPointToRay(mouseScreenPosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero); // Assuming the ground is at y = 0
        float rayLength;

        if (groundPlane.Raycast(ray, out rayLength))
        {
            Vector3 pointToLook = ray.GetPoint(rayLength);

            // Calculate the direction from the pivot to the mouse position
            Vector3 direction = pointToLook - armPivotR.position;

            // Calculate the rotation required to look at the mouse position
            Quaternion targetRotation = Quaternion.LookRotation(direction);

            // Smoothly interpolate the rotation for better visual effect
            armPivotR.rotation = Quaternion.Slerp(armPivotR.rotation, targetRotation, armRotationSpeed * Time.deltaTime);
            //armPivotL.rotation = Quaternion.Slerp(armPivotL.rotation, targetRotation, armRotationSpeed * Time.deltaTime);
        }
    }
#endif

#if !UNITY_SERVER
    [ClientCallback]
    void Pickup()
    {
        if (canPickup && pickedUpObject == null)
        {
            pickedUpObject = playerArmCollision.collidedGameObject;
            pickedUpObjectRigidbody = pickedUpObject.GetComponent<Rigidbody>();
            pickedUpObject.SetParent(armPivotR);
            //pickedUpObjectRigidbody.isKinematic = true;
            pickedUpObjectRigidbody.useGravity = false;
            pickedUpObjectRigidbody.constraints = RigidbodyConstraints.FreezePosition;
            playerArmCollision.triggerCollider.enabled = false;
            canPickup = false;
        }
        else if(pickedUpObject != null)
        {
            pickedUpObject.SetParent(null);
            //pickedUpObjectRigidbody.isKinematic = false;
            pickedUpObjectRigidbody.useGravity = true;
            pickedUpObjectRigidbody.constraints = RigidbodyConstraints.None;
            playerArmCollision.triggerCollider.enabled = true;
            pickedUpObject = null;
        }
    }
#endif
}
