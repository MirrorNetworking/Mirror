using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class PlayerPickupParty : NetworkBehaviour
{

    public float moveSpeed = 8f;
    public float jumpSpeed = 8f;
    public float gravity = 20f;

    private Vector3 movement = Vector3.zero;
    private float verticalSpeed = 0f;
    public float armRotationSpeed = 25f;
    public bool canPickup = false;
    public Transform[] pickedUpObjects; // store pickups like an inventory
    public Rigidbody[] pickedUpRigidbodies; // cache pickups to avoid re-referencing and get components
    public Transform[] armPivots; // acts like inventory slots
    public PlayerArmSlot[] playerArmSlots; // acts like inventory slots
    public bool freezeRotationRB = true; // if false, picked up objects rotate with collision

    public CharacterController characterController;
    private Camera mainCamera;
    private int slotActive = 0; // 0 right arm, 1 left arm


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

        PlayerMovement();
        RotatePlayerToMouse();
        RotateArmsToMouse();

        if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E))
        {
            Pickup();
        }

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Q))
        {
            if (slotActive == 0)
            {
                slotActive = 1;
            }
            else
            {
                slotActive = 0;
            }
        }
    }
#endif

#if !UNITY_SERVER
    [ClientCallback]
    void PlayerMovement()
    {
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");

        Vector3 horizontalMovement = new Vector3(moveHorizontal, 0f, moveVertical);
        if (horizontalMovement.magnitude > 1f) horizontalMovement.Normalize();
        horizontalMovement *= moveSpeed;

        if (characterController.isGrounded)
        {
            verticalSpeed = -gravity * Time.deltaTime;
            if (Input.GetKeyDown(KeyCode.Space))
            {
                verticalSpeed = jumpSpeed;
            }
        }
        else
        {
            verticalSpeed -= gravity * Time.deltaTime;
        }

        movement = horizontalMovement;
        movement.y = verticalSpeed;
        characterController.Move(movement * Time.deltaTime);
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
    void RotateArmsToMouse()
    {
        if (slotActive == 0)
        {
            Quaternion defaultArmRotation = Quaternion.Euler(-45, -90, 0);
            armPivots[1].localRotation = Quaternion.Slerp(armPivots[1].localRotation, defaultArmRotation, (armRotationSpeed / 5) * Time.deltaTime);
        }
        else
        {
            Quaternion defaultArmRotation = Quaternion.Euler(-45, 90, 0);
            armPivots[0].localRotation = Quaternion.Slerp(armPivots[0].localRotation, defaultArmRotation, (armRotationSpeed / 5) * Time.deltaTime);
        }

        Vector3 mouseScreenPosition = Input.mousePosition;
        Ray ray = mainCamera.ScreenPointToRay(mouseScreenPosition);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        float rayLength;

        if (groundPlane.Raycast(ray, out rayLength))
        {
            Vector3 pointToLook = ray.GetPoint(rayLength);
            Vector3 direction = pointToLook - armPivots[slotActive].position;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            armPivots[slotActive].rotation = Quaternion.Slerp(armPivots[slotActive].rotation, targetRotation, armRotationSpeed * Time.deltaTime);
        }
    }
#endif

#if !UNITY_SERVER
    [ClientCallback]
    void Pickup()
    {
        if (canPickup && pickedUpObjects[slotActive] == null)
        {
            pickedUpObjects[slotActive] = playerArmSlots[slotActive].collidedGameObject;
            pickedUpRigidbodies[slotActive] = pickedUpObjects[slotActive].GetComponent<Rigidbody>();
            pickedUpObjects[slotActive].SetParent(armPivots[slotActive]);
            //pickedUpObjectRigidbody.isKinematic = true;
            pickedUpRigidbodies[slotActive].useGravity = false;
            if (freezeRotationRB)
            {
                pickedUpRigidbodies[slotActive].constraints = RigidbodyConstraints.FreezeRotation;
            }
            playerArmSlots[slotActive].triggerCollider.enabled = false;
            canPickup = false;
        }
        else if (pickedUpObjects[slotActive] != null)
        {
            pickedUpObjects[slotActive].SetParent(null);
            //pickedUpObjectRigidbody.isKinematic = false;
            pickedUpRigidbodies[slotActive].useGravity = true;
            if (freezeRotationRB)
            {
                pickedUpRigidbodies[slotActive].constraints = RigidbodyConstraints.None;
            }
            playerArmSlots[slotActive].triggerCollider.enabled = true;
            pickedUpObjects[slotActive] = null;
        }
    }
#endif
}
