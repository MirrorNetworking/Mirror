using System;
using UnityEngine;
using Mirror;

namespace TestNT
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CharacterController))]
    //[RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(NTReliableExt))]
    public class PlayerMove : NetworkBehaviour
    {
        public enum GroundState : byte { Jumping, Falling, Grounded }
        public enum MoveMode : byte { Walking, Sneaking, Running };

        [Header("Avatar Components")]
        public CharacterController characterController;
        public NTReliableExt NTReliableExt;
        public Animator animator;

        [Header("Materials")]
        public PhysicMaterial physicsMaterial;

        [Header("Movement")]
        [Range(1, 20)]
        public float moveSpeedMultiplier = 8f;

        [Header("Turning")]
        [Range(1f, 200f)]
        public float maxTurnSpeed = 100f;
        [Range(.5f, 5f)]
        public float turnDelta = 3f;

        [Header("Jumping")]
        [Range(0.1f, 1f)]
        public float initialJumpSpeed = 0.2f;
        [Range(1f, 10f)]
        public float maxJumpSpeed = 5f;
        [Range(0.1f, 1f)]
        public float jumpDelta = 0.2f;

        [Header("Diagnostics - Do Not Modify")]
        public GroundState groundState = GroundState.Grounded;
        public MoveMode moveState = MoveMode.Running;

        [Range(-1f, 1f)]
        public float horizontal;
        [Range(-1f, 1f)]
        public float vertical;

        [Range(-200f, 200f)]
        public float turnSpeed;

        [Range(-10f, 10f)]
        public float jumpSpeed;

        [Range(-1.5f, 1.5f)]
        public float animVelocity;

        [Range(-1.5f, 1.5f)]
        public float animRotation;

        public Vector3Int velocity;
        public Vector3 direction;

        void OnValidate()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            // Override CharacterController default values
            characterController.enabled = false;
            characterController.skinWidth = 0.02f;
            characterController.minMoveDistance = 0f;
            characterController.sharedMaterial = physicsMaterial;

            GetComponent<Rigidbody>().isKinematic = true;

            if (NTReliableExt == null)
                NTReliableExt = GetComponent<NTReliableExt>();

            this.enabled = false;
        }

        public override void OnStartClient()
        {
            if (!isLocalPlayer)
                NTReliableExt.VelRotChangedAction = OnVelRotChanged;
        }

        public override void OnStopClient()
        {
            NTReliableExt.VelRotChangedAction = null;
        }

        public override void OnStartAuthority()
        {
            characterController.enabled = true;
            this.enabled = true;
        }

        public override void OnStopAuthority()
        {
            this.enabled = false;
            characterController.enabled = false;
        }

        void Update()
        {
            if (!characterController.enabled)
                return;

#if !UNITY_SERVER
            // Not needed on headless clients
            HandleMoveState();
#endif

            HandleTurning();
            HandleJumping();
            HandleMove();

            // Reset ground state
            if (characterController.isGrounded)
                groundState = GroundState.Grounded;
            else if (groundState != GroundState.Jumping)
                groundState = GroundState.Falling;

            // Diagnostic velocity...FloorToInt for display purposes
            velocity = Vector3Int.FloorToInt(characterController.velocity);
        }

        // Headless clients don't need to do either of these
#if !UNITY_SERVER

        void HandleMoveState()
        {
            if (Input.GetKeyUp(KeyCode.R) && moveState == MoveMode.Walking)
                moveState = MoveMode.Running;
            else if (Input.GetKeyUp(KeyCode.R) && moveState == MoveMode.Running)
                moveState = MoveMode.Walking;
            else if (Input.GetKeyUp(KeyCode.C) && moveState != MoveMode.Sneaking)
                moveState = MoveMode.Sneaking;
            else if (Input.GetKeyUp(KeyCode.C) && moveState == MoveMode.Sneaking)
                moveState = MoveMode.Walking;
        }

#endif


        // Alternative methods provided for headless clients to act autonomously
#if !UNITY_SERVER

        // TODO: Turning works while airborne...feature?
        void HandleTurning()
        {
            // Q and E cancel each other out, reducing the turn to zero.
            if (Input.GetKey(KeyCode.Q))
                turnSpeed = Mathf.MoveTowards(turnSpeed, -maxTurnSpeed, turnDelta);
            if (Input.GetKey(KeyCode.E))
                turnSpeed = Mathf.MoveTowards(turnSpeed, maxTurnSpeed, turnDelta);

            // If both pressed, reduce turning speed toward zero.
            if (Input.GetKey(KeyCode.Q) && Input.GetKey(KeyCode.E))
                turnSpeed = Mathf.MoveTowards(turnSpeed, 0, turnDelta);

            // If neither pressed, reduce turning speed toward zero.
            if (!Input.GetKey(KeyCode.Q) && !Input.GetKey(KeyCode.E))
                turnSpeed = Mathf.MoveTowards(turnSpeed, 0, turnDelta);

            if (moveState == MoveMode.Sneaking)
                turnSpeed /= 3;

            transform.Rotate(0f, turnSpeed * Time.deltaTime, 0f);
        }

        void HandleJumping()
        {
            // Handle variable force jumping.
            // Jump starts with initial power on takeoff, and jumps higher / longer
            // as player holds spacebar. Jump power is increased by a diminishing amout
            // every frame until it reaches maxJumpSpeed, or player releases the spacebar,
            // and then changes to the falling state until it gets grounded.
            if (groundState != GroundState.Falling && moveState != MoveMode.Sneaking && Input.GetKey(KeyCode.Space))
            {
                if (groundState != GroundState.Jumping)
                {
                    // Start jump at initial power.
                    groundState = GroundState.Jumping;
                    jumpSpeed = initialJumpSpeed;
                }
                else
                    // Jumping has already started...increase power toward maxJumpSpeed over time.
                    jumpSpeed = Mathf.MoveTowards(jumpSpeed, maxJumpSpeed, jumpDelta);

                // If power has reached maxJumpSpeed, change to falling until grounded.
                // This prevents over-applying jump power while already in the air.
                if (jumpSpeed == maxJumpSpeed)
                    groundState = GroundState.Falling;
            }
            else if (groundState != GroundState.Grounded)
            {
                // handles running off a cliff and/or player released Spacebar.
                groundState = GroundState.Falling;
                jumpSpeed = Mathf.Min(jumpSpeed, maxJumpSpeed);
                jumpSpeed += Physics.gravity.y * Time.deltaTime;
            }
            else
                jumpSpeed = Physics.gravity.y * Time.deltaTime;
        }

#else

        // Headless client forced to a slow constant turn
        void HandleTurning()
        {
            turnSpeed = maxTurnSpeed * 0.5f;
            transform.Rotate(0f, turnSpeed * Time.deltaTime, 0f);
        }

        // Headless client forced to ground
        void HandleJumping()
        {
            jumpSpeed = Physics.gravity.y * Time.deltaTime;
        }

#endif

        // TODO: Directional input works while airborne...feature?
        void HandleMove()
        {
            // Capture inputs
#if !UNITY_SERVER
            horizontal = Input.GetAxis("Horizontal");
            vertical = Input.GetAxis("Vertical");
#else
            // Headless client running forward
            horizontal = 0f;
            vertical = 1f;
#endif
            // Create initial direction vector without jumpSpeed (y-axis).
            direction = new Vector3(horizontal, 0f, vertical);

            // Run unless Sneaking or Walking
            if (moveState == MoveMode.Sneaking)
                direction *= 0.15f;
            else if (moveState == MoveMode.Walking)
                direction *= 0.5f;

            // Clamp so diagonal strafing isn't a speed advantage.
            direction = Vector3.ClampMagnitude(direction, 1f);

            // Transforms direction from local space to world space.
            direction = transform.TransformDirection(direction);

            // Multiply for desired ground speed.
            direction *= moveSpeedMultiplier;

            // Add jumpSpeed to direction as last step.
            direction.y = jumpSpeed;

            // Finally move the character.
            characterController.Move(direction * Time.deltaTime);
        }

        void OnVelRotChanged(Vector3 newVelocity, Vector3 newRotation)
        {
            // Only apply to other player objects
            if (isLocalPlayer) return;

            animVelocity = -transform.InverseTransformDirection(newVelocity).z / moveSpeedMultiplier;
            animRotation = -newRotation.y / maxTurnSpeed;

            if (animator)
            {
                animator.SetFloat("Forward", Mathf.MoveTowards(animator.GetFloat("Forward"), animVelocity, moveSpeedMultiplier * Time.deltaTime));
                animator.SetFloat("Turn", Mathf.MoveTowards(animator.GetFloat("Turn"), animRotation, maxTurnSpeed * Time.deltaTime));
            }
        }
    }
}
