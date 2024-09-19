using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Mirror.Examples.Common.Controllers.Player
{
    [AddComponentMenu("")]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkIdentity))]
    [DisallowMultipleComponent]
    public class PlayerControllerBase : NetworkBehaviour
    {
        const float BASE_DPI = 96f;

        public enum GroundState : byte { Jumping, Falling, Grounded }

        [Serializable]
        public struct MoveKeys
        {
            public KeyCode Forward;
            public KeyCode Back;
            public KeyCode StrafeLeft;
            public KeyCode StrafeRight;
            public KeyCode TurnLeft;
            public KeyCode TurnRight;
            public KeyCode Jump;
        }

        [Serializable]
        public struct OptionsKeys
        {
            public KeyCode MouseSteer;
            public KeyCode AutoRun;
            public KeyCode ToggleUI;
        }

        [Flags]
        public enum ControlOptions : byte
        {
            None,
            MouseSteer = 1 << 0,
            AutoRun = 1 << 1,
            ShowUI = 1 << 2
        }

        [Header("Avatar Components")]
        public CharacterController characterController;

        [Header("User Interface")]
        public GameObject ControllerUIPrefab;

        [Header("Configuration")]
        [SerializeField]
        public MoveKeys moveKeys = new MoveKeys
        {
            Forward = KeyCode.W,
            Back = KeyCode.S,
            StrafeLeft = KeyCode.A,
            StrafeRight = KeyCode.D,
            TurnLeft = KeyCode.Q,
            TurnRight = KeyCode.E,
            Jump = KeyCode.Space,
        };

        [SerializeField]
        public OptionsKeys optionsKeys = new OptionsKeys
        {
            MouseSteer = KeyCode.M,
            AutoRun = KeyCode.R,
            ToggleUI = KeyCode.U
        };

        [Space(5)]
        public ControlOptions controlOptions = ControlOptions.ShowUI;

        [Header("Movement")]
        [Range(0, 20)]
        [FormerlySerializedAs("moveSpeedMultiplier")]
        [Tooltip("Speed in meters per second")]
        public float maxMoveSpeed = 8f;

        // Replacement for Sensitvity from Input Settings.
        [Range(0, 10f)]
        [Tooltip("Sensitivity factors into accelleration")]
        public float inputSensitivity = 2f;

        // Replacement for Gravity from Input Settings.
        [Range(0, 10f)]
        [Tooltip("Gravity factors into decelleration")]
        public float inputGravity = 2f;

        [Header("Turning")]
        [Range(0, 300f)]
        [Tooltip("Max Rotation in degrees per second")]
        public float maxTurnSpeed = 100f;
        [Range(0, 10f)]
        [FormerlySerializedAs("turnDelta")]
        [Tooltip("Rotation acceleration in degrees per second squared")]
        public float turnAcceleration = 3f;

        [Header("Jumping")]
        [Range(0, 10f)]
        [Tooltip("Initial jump speed in meters per second")]
        public float initialJumpSpeed = 2.5f;
        [Range(0, 10f)]
        [Tooltip("Maximum jump speed in meters per second")]
        public float maxJumpSpeed = 3.5f;
        [Range(0, 10f)]
        [FormerlySerializedAs("jumpDelta")]
        [Tooltip("Jump acceleration in meters per second squared")]
        public float jumpAcceleration = 4f;

        [Header("Diagnostics")]
        [ReadOnly, SerializeField]
        GroundState groundState = GroundState.Grounded;

        [ReadOnly, SerializeField, Range(-1f, 1f)]
        float horizontal;
        [ReadOnly, SerializeField, Range(-1f, 1f)]
        float vertical;

        [ReadOnly, SerializeField, Range(-1f, 1f)]
        float mouseInputX;
        [ReadOnly, SerializeField, Range(0, 30f)]
        float mouseSensitivity;
        [ReadOnly, SerializeField, Range(-300f, 300f)]
        float turnSpeed;

        [ReadOnly, SerializeField, Range(-10f, 10f)]
        float jumpSpeed;

        [ReadOnly, SerializeField, Range(-1.5f, 1.5f)]
        float animVelocity;

        [ReadOnly, SerializeField, Range(-1.5f, 1.5f)]
        float animRotation;

        [ReadOnly, SerializeField]
        Vector3 direction;

        [ReadOnly, SerializeField]
        Vector3Int velocity;

        [ReadOnly, SerializeField]
        GameObject controllerUI;

        #region Network Setup

        protected override void OnValidate()
        {
            // Skip if Editor is in Play mode
            if (Application.isPlaying) return;

            base.OnValidate();
            Reset();
        }

        void Reset()
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            // Override CharacterController default values
            characterController.enabled = false;
            characterController.skinWidth = 0.02f;
            characterController.minMoveDistance = 0f;

            GetComponent<Rigidbody>().isKinematic = true;

#if UNITY_EDITOR
            // For convenience in the examples, we use the GUID of the PlayerControllerUI
            // to find the correct prefab in the Mirror/Examples/_Common/Controllers folder.
            // This avoids conflicts with user-created prefabs that may have the same name
            // and avoids polluting the user's project with Resources.
            // This is not recommended for production code...use Resources.Load or AssetBundles instead.
            if (ControllerUIPrefab == null)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath("7beee247444994f0281dadde274cc4af");
                ControllerUIPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
#endif

            this.enabled = false;
        }

        void OnDisable()
        {
            horizontal = 0f;
            vertical = 0f;
            turnSpeed = 0f;
        }

        public override void OnStartAuthority()
        {
            // Calculate DPI-aware sensitivity
            float dpiScale = (Screen.dpi > 0) ? (Screen.dpi / BASE_DPI) : 1f;
            mouseSensitivity = turnAcceleration * dpiScale;
            //Debug.Log($"Screen DPI: {Screen.dpi}, DPI Scale: {dpiScale}, Adjusted Turn Acceleration: {turnAccelerationDPI}");

            SetCursor(controlOptions.HasFlag(ControlOptions.MouseSteer));

            characterController.enabled = true;
            this.enabled = true;
        }

        public override void OnStopAuthority()
        {
            this.enabled = false;
            characterController.enabled = false;
            SetCursor(false);
        }

        public override void OnStartLocalPlayer()
        {
            if (ControllerUIPrefab != null)
                controllerUI = Instantiate(ControllerUIPrefab);

            if (controllerUI != null)
            {
                if (controllerUI.TryGetComponent(out PlayerControllerUI canvasControlPanel))
                    canvasControlPanel.Refresh(moveKeys, optionsKeys);

                controllerUI.SetActive(controlOptions.HasFlag(ControlOptions.ShowUI));
            }
        }

        public override void OnStopLocalPlayer()
        {
            if (controllerUI != null)
                Destroy(controllerUI);
            controllerUI = null;
        }

        #endregion

        void Update()
        {
            if (!characterController.enabled)
                return;

            float deltaTime = Time.deltaTime;

            HandleOptions();

            if (controlOptions.HasFlag(ControlOptions.MouseSteer))
                HandleMouseSteer(deltaTime);
            else
                HandleTurning(deltaTime);

            HandleJumping(deltaTime);
            HandleMove(deltaTime);
            ApplyMove(deltaTime);

            // Reset ground state
            if (characterController.isGrounded)
                groundState = GroundState.Grounded;
            else if (groundState != GroundState.Jumping)
                groundState = GroundState.Falling;

            // Diagnostic velocity...FloorToInt for display purposes
            velocity = Vector3Int.FloorToInt(characterController.velocity);
        }

        void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void HandleOptions()
        {
            if (optionsKeys.MouseSteer != KeyCode.None && Input.GetKeyUp(optionsKeys.MouseSteer))
            {
                controlOptions ^= ControlOptions.MouseSteer;
                SetCursor(controlOptions.HasFlag(ControlOptions.MouseSteer));
            }

            if (optionsKeys.AutoRun != KeyCode.None && Input.GetKeyUp(optionsKeys.AutoRun))
                controlOptions ^= ControlOptions.AutoRun;

            if (optionsKeys.ToggleUI != KeyCode.None && Input.GetKeyUp(optionsKeys.ToggleUI))
            {
                controlOptions ^= ControlOptions.ShowUI;

                if (controllerUI != null)
                    controllerUI.SetActive(controlOptions.HasFlag(ControlOptions.ShowUI));
            }
        }

        // Turning works while airborne...feature?
        void HandleTurning(float deltaTime)
        {
            float targetTurnSpeed = 0f;

            // TurnLeft and TurnRight cancel each other out, reducing targetTurnSpeed to zero.
            if (moveKeys.TurnLeft != KeyCode.None && Input.GetKey(moveKeys.TurnLeft))
                targetTurnSpeed -= maxTurnSpeed;
            if (moveKeys.TurnRight != KeyCode.None && Input.GetKey(moveKeys.TurnRight))
                targetTurnSpeed += maxTurnSpeed;

            // If there's turn input or AutoRun is not enabled, adjust turn speed towards target
            // If no turn input and AutoRun is enabled, maintain the previous turn speed
            if (targetTurnSpeed != 0f || !controlOptions.HasFlag(ControlOptions.AutoRun))
                turnSpeed = Mathf.MoveTowards(turnSpeed, targetTurnSpeed, turnAcceleration * maxTurnSpeed * deltaTime);

            transform.Rotate(0f, turnSpeed * deltaTime, 0f);
        }

        void HandleMouseSteer(float deltaTime)
        {
            // Accumulate mouse input over time
            mouseInputX += Input.GetAxisRaw("Mouse X") * mouseSensitivity;

            // Clamp the accumulator to simulate key press behavior
            mouseInputX = Mathf.Clamp(mouseInputX, -1f, 1f);

            // Calculate target turn speed
            float targetTurnSpeed = mouseInputX * maxTurnSpeed;

            // Use the same acceleration logic as HandleTurning
            turnSpeed = Mathf.MoveTowards(turnSpeed, targetTurnSpeed, mouseSensitivity * maxTurnSpeed * deltaTime);

            // Apply rotation
            transform.Rotate(0f, turnSpeed * deltaTime, 0f);

            mouseInputX = Mathf.MoveTowards(mouseInputX, 0f, mouseSensitivity * deltaTime);
        }

        void HandleJumping(float deltaTime)
        {
            if (groundState != GroundState.Falling && moveKeys.Jump != KeyCode.None && Input.GetKey(moveKeys.Jump))
            {
                if (groundState != GroundState.Jumping)
                {
                    groundState = GroundState.Jumping;
                    jumpSpeed = initialJumpSpeed;
                }
                else if (jumpSpeed < maxJumpSpeed)
                {
                    // Increase jumpSpeed using a square root function for a fast start and slow finish
                    float jumpProgress = (jumpSpeed - initialJumpSpeed) / (maxJumpSpeed - initialJumpSpeed);
                    jumpSpeed += (jumpAcceleration * Mathf.Sqrt(1 - jumpProgress)) * deltaTime;
                }

                if (jumpSpeed >= maxJumpSpeed)
                {
                    jumpSpeed = maxJumpSpeed;
                    groundState = GroundState.Falling;
                }
            }
            else if (groundState != GroundState.Grounded)
            {
                groundState = GroundState.Falling;
                jumpSpeed = Mathf.Min(jumpSpeed, maxJumpSpeed);
                jumpSpeed += Physics.gravity.y * deltaTime;
            }
            else
                // maintain small downward speed for when falling off ledges
                jumpSpeed = Physics.gravity.y * deltaTime;
        }

        void HandleMove(float deltaTime)
        {
            // Initialize target movement variables
            float targetMoveX = 0f;
            float targetMoveZ = 0f;

            // Check for WASD key presses and adjust target movement variables accordingly
            if (moveKeys.Forward != KeyCode.None && Input.GetKey(moveKeys.Forward)) targetMoveZ = 1f;
            if (moveKeys.Back != KeyCode.None && Input.GetKey(moveKeys.Back)) targetMoveZ = -1f;
            if (moveKeys.StrafeLeft != KeyCode.None && Input.GetKey(moveKeys.StrafeLeft)) targetMoveX = -1f;
            if (moveKeys.StrafeRight != KeyCode.None && Input.GetKey(moveKeys.StrafeRight)) targetMoveX = 1f;

            if (targetMoveX == 0f)
            {
                if (!controlOptions.HasFlag(ControlOptions.AutoRun))
                    horizontal = Mathf.MoveTowards(horizontal, targetMoveX, inputGravity * deltaTime);
            }
            else
                horizontal = Mathf.MoveTowards(horizontal, targetMoveX, inputSensitivity * deltaTime);

            if (targetMoveZ == 0f)
            {
                if (!controlOptions.HasFlag(ControlOptions.AutoRun))
                    vertical = Mathf.MoveTowards(vertical, targetMoveZ, inputGravity * deltaTime);
            }
            else
                vertical = Mathf.MoveTowards(vertical, targetMoveZ, inputSensitivity * deltaTime);
        }

        void ApplyMove(float deltaTime)
        {
            // Create initial direction vector without jumpSpeed (y-axis).
            direction = new Vector3(horizontal, 0f, vertical);

            // Clamp so diagonal strafing isn't a speed advantage.
            direction = Vector3.ClampMagnitude(direction, 1f);

            // Transforms direction from local space to world space.
            direction = transform.TransformDirection(direction);

            // Multiply for desired ground speed.
            direction *= maxMoveSpeed;

            // Add jumpSpeed to direction as last step.
            direction.y = jumpSpeed;

            // Finally move the character.
            characterController.Move(direction * deltaTime);
        }
    }
}
