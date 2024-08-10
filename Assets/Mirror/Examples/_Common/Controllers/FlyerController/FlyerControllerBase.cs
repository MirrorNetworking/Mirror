using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Mirror.Examples.Common.Controllers.Flyer
{
    [AddComponentMenu("")]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkIdentity))]
    [DisallowMultipleComponent]
    public class FlyerControllerBase : NetworkBehaviour
    {
        const float BASE_DPI = 96f;

        [Serializable]
        public struct OptionsKeys
        {
            public KeyCode MouseSteer;
            public KeyCode AutoRun;
            public KeyCode ToggleUI;
        }

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
        }

        [Serializable]
        public struct FlightKeys
        {
            public KeyCode PitchDown;
            public KeyCode PitchUp;
            public KeyCode RollLeft;
            public KeyCode RollRight;
            public KeyCode AutoLevel;
        }

        [Flags]
        public enum ControlOptions : byte
        {
            None,
            MouseSteer = 1 << 0,
            AutoRun = 1 << 1,
            AutoLevel = 1 << 2,
            ShowUI = 1 << 3
        }

        [Header("Avatar Components")]
        public CapsuleCollider capsuleCollider;
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
            TurnRight = KeyCode.E
        };

        [SerializeField]
        public FlightKeys flightKeys = new FlightKeys
        {
            PitchDown = KeyCode.UpArrow,
            PitchUp = KeyCode.DownArrow,
            RollLeft = KeyCode.LeftArrow,
            RollRight = KeyCode.RightArrow,
            AutoLevel = KeyCode.L
        };

        [SerializeField]
        public OptionsKeys optionsKeys = new OptionsKeys
        {
            MouseSteer = KeyCode.M,
            AutoRun = KeyCode.R,
            ToggleUI = KeyCode.U
        };

        [Space(5)]
        public ControlOptions controlOptions = ControlOptions.AutoLevel | ControlOptions.ShowUI;

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

        [Header("Pitch")]
        [Range(0, 180f)]
        [Tooltip("Max Pitch in degrees per second")]
        public float maxPitchSpeed = 30f;
        [Range(0, 180f)]
        [Tooltip("Max Pitch in degrees")]
        public float maxPitchUpAngle = 20f;
        [Range(0, 180f)]
        [Tooltip("Max Pitch in degrees")]
        public float maxPitchDownAngle = 45f;
        [Range(0, 10f)]
        [Tooltip("Pitch acceleration in degrees per second squared")]
        public float pitchAcceleration = 3f;

        [Header("Roll")]
        [Range(0, 180f)]
        [Tooltip("Max Roll in degrees per second")]
        public float maxRollSpeed = 30f;
        [Range(0, 180f)]
        [Tooltip("Max Roll in degrees")]
        public float maxRollAngle = 45f;
        [Range(0, 10f)]
        [Tooltip("Roll acceleration in degrees per second squared")]
        public float rollAcceleration = 3f;

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

        [ReadOnly, SerializeField, Range(-180f, 180f)]
        float pitchAngle;
        [ReadOnly, SerializeField, Range(-180f, 180f)]
        float pitchSpeed;

        [ReadOnly, SerializeField, Range(-180f, 180f)]
        float rollAngle;
        [ReadOnly, SerializeField, Range(-180f, 180f)]
        float rollSpeed;

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
            if (capsuleCollider == null)
                capsuleCollider = GetComponent<CapsuleCollider>();

            // Enable by default...it will be disabled when characterController is enabled
            capsuleCollider.enabled = true;

            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            // Override CharacterController default values
            characterController.enabled = false;
            characterController.skinWidth = 0.02f;
            characterController.minMoveDistance = 0f;

            GetComponent<Rigidbody>().isKinematic = true;

#if UNITY_EDITOR
            // For convenience in the examples, we use the GUID of the FlyerControllerUI
            // to find the correct prefab in the Mirror/Examples/_Common/Controllers folder.
            // This avoids conflicts with user-created prefabs that may have the same name
            // and avoids polluting the user's project with Resources.
            // This is not recommended for production code...use Resources.Load or AssetBundles instead.
            if (ControllerUIPrefab == null)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath("493615025d304c144bacfb91f6aac90e");
                ControllerUIPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
#endif

            this.enabled = false;
        }

        public override void OnStartAuthority()
        {
            // Calculate DPI-aware sensitivity
            float dpiScale = (Screen.dpi > 0) ? (Screen.dpi / BASE_DPI) : 1f;
            mouseSensitivity = turnAcceleration * dpiScale;
            //Debug.Log($"Screen DPI: {Screen.dpi}, DPI Scale: {dpiScale}, Adjusted Turn Acceleration: {turnAccelerationDPI}");

            SetCursor(controlOptions.HasFlag(ControlOptions.MouseSteer));

            // capsuleCollider and characterController are mutually exclusive
            // Having both enabled would double fire triggers and other collisions
            capsuleCollider.enabled = false;
            characterController.enabled = true;
            this.enabled = true;
        }

        public override void OnStopAuthority()
        {
            this.enabled = false;

            // capsuleCollider and characterController are mutually exclusive
            // Having both enabled would double fire triggers and other collisions
            capsuleCollider.enabled = true;
            characterController.enabled = false;

            SetCursor(false);
        }

        public override void OnStartLocalPlayer()
        {
            if (ControllerUIPrefab != null)
                controllerUI = Instantiate(ControllerUIPrefab);

            if (controllerUI != null)
            {
                if (controllerUI.TryGetComponent(out FlyerControllerUI canvasControlPanel))
                    canvasControlPanel.Refresh(moveKeys, flightKeys, optionsKeys);

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
            if (!Application.isFocused)
                return;

            if (!characterController.enabled)
                return;

            float deltaTime = Time.deltaTime;

            HandleOptions();

            if (controlOptions.HasFlag(ControlOptions.MouseSteer))
                HandleMouseSteer(deltaTime);
            else
                HandleTurning(deltaTime);

            HandlePitch(deltaTime);
            HandleRoll(deltaTime);
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

            if (flightKeys.AutoLevel != KeyCode.None && Input.GetKeyUp(flightKeys.AutoLevel))
                controlOptions ^= ControlOptions.AutoLevel;
        }

        // Turning works while airborne...feature?
        void HandleTurning(float deltaTime)
        {
            float targetTurnSpeed = 0f;

            // Q and E cancel each other out, reducing targetTurnSpeed to zero.
            if (moveKeys.TurnLeft != KeyCode.None && Input.GetKey(moveKeys.TurnLeft))
                targetTurnSpeed -= maxTurnSpeed;
            if (moveKeys.TurnRight != KeyCode.None && Input.GetKey(moveKeys.TurnRight))
                targetTurnSpeed += maxTurnSpeed;

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

        void HandlePitch(float deltaTime)
        {
            float targetPitchSpeed = 0f;
            bool inputDetected = false;

            // Up and Down arrows for pitch
            if (flightKeys.PitchUp != KeyCode.None && Input.GetKey(flightKeys.PitchUp))
            {
                targetPitchSpeed -= maxPitchSpeed;
                inputDetected = true;
            }

            if (flightKeys.PitchDown != KeyCode.None && Input.GetKey(flightKeys.PitchDown))
            {
                targetPitchSpeed += maxPitchSpeed;
                inputDetected = true;
            }

            pitchSpeed = Mathf.MoveTowards(pitchSpeed, targetPitchSpeed, pitchAcceleration * maxPitchSpeed * deltaTime);

            // Apply pitch rotation
            pitchAngle += pitchSpeed * deltaTime;
            pitchAngle = Mathf.Clamp(pitchAngle, -maxPitchUpAngle, maxPitchDownAngle);

            // Return to zero when no input
            if (!inputDetected && controlOptions.HasFlag(ControlOptions.AutoLevel))
                pitchAngle = Mathf.MoveTowards(pitchAngle, 0f, maxPitchSpeed * deltaTime);

            ApplyRotation();
        }

        void HandleRoll(float deltaTime)
        {
            float targetRollSpeed = 0f;
            bool inputDetected = false;

            // Left and Right arrows for roll
            if (flightKeys.RollRight != KeyCode.None && Input.GetKey(flightKeys.RollRight))
            {
                targetRollSpeed -= maxRollSpeed;
                inputDetected = true;
            }

            if (flightKeys.RollLeft != KeyCode.None && Input.GetKey(flightKeys.RollLeft))
            {
                targetRollSpeed += maxRollSpeed;
                inputDetected = true;
            }

            rollSpeed = Mathf.MoveTowards(rollSpeed, targetRollSpeed, rollAcceleration * maxRollSpeed * deltaTime);

            // Apply roll rotation
            rollAngle += rollSpeed * deltaTime;
            rollAngle = Mathf.Clamp(rollAngle, -maxRollAngle, maxRollAngle);

            // Return to zero when no input
            if (!inputDetected && controlOptions.HasFlag(ControlOptions.AutoLevel))
                rollAngle = Mathf.MoveTowards(rollAngle, 0f, maxRollSpeed * deltaTime);

            ApplyRotation();
        }

        void ApplyRotation()
        {
            // Get the current yaw (Y-axis rotation)
            float currentYaw = transform.localRotation.eulerAngles.y;

            // Apply all rotations
            transform.localRotation = Quaternion.Euler(pitchAngle, currentYaw, rollAngle);
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

            //// Add jumpSpeed to direction as last step.
            //direction.y = jumpSpeed;

            // Finally move the character.
            characterController.Move(direction * deltaTime);
        }
    }
}