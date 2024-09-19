using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Mirror.Examples.Common.Controllers.Tank
{
    [AddComponentMenu("")]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(TankHealth))]
    [DisallowMultipleComponent]
    public class TankControllerBase : NetworkBehaviour
    {
        public enum GroundState : byte { Jumping, Falling, Grounded }

        [Serializable]
        public struct MoveKeys
        {
            public KeyCode Forward;
            public KeyCode Back;
            public KeyCode TurnLeft;
            public KeyCode TurnRight;
        }

        [Serializable]
        public struct OptionsKeys
        {
            public KeyCode AutoRun;
            public KeyCode ToggleUI;
        }

        [Flags]
        public enum ControlOptions : byte
        {
            None,
            AutoRun = 1 << 0,
            ShowUI = 1 << 1
        }

        [Header("Components")]
        public BoxCollider boxCollider;
        public CharacterController characterController;

        [Header("User Interface")]
        public GameObject ControllerUIPrefab;

        [Header("Configuration")]
        [SerializeField]
        public MoveKeys moveKeys = new MoveKeys
        {
            Forward = KeyCode.W,
            Back = KeyCode.S,
            TurnLeft = KeyCode.A,
            TurnRight = KeyCode.D,
        };

        [SerializeField]
        public OptionsKeys optionsKeys = new OptionsKeys
        {
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

        [Header("Diagnostics")]
        [ReadOnly, SerializeField]
        GroundState groundState = GroundState.Grounded;

        [ReadOnly, SerializeField, Range(-1f, 1f)]
        float horizontal;
        [ReadOnly, SerializeField, Range(-1f, 1f)]
        float vertical;

        [ReadOnly, SerializeField, Range(-300f, 300f)]
        float turnSpeed;

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

        protected virtual void Reset()
        {
            if (boxCollider == null)
                boxCollider = GetComponentInChildren<BoxCollider>();

            // Enable by default...it will be disabled when characterController is enabled
            boxCollider.enabled = true;

            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            // Override CharacterController default values
            characterController.enabled = false;
            characterController.skinWidth = 0.02f;
            characterController.minMoveDistance = 0f;

            GetComponent<Rigidbody>().isKinematic = true;

#if UNITY_EDITOR
            // For convenience in the examples, we use the GUID of the TankControllerUI prefab
            // to find the correct prefab in the Mirror/Examples/_Common/Controllers folder.
            // This avoids conflicts with user-created prefabs that may have the same name
            // and avoids polluting the user's project with Resources.
            // This is not recommended for production code...use Resources.Load or AssetBundles instead.
            if (ControllerUIPrefab == null)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath("e64b14552402f6745a7f0aca6237fae2");
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
            // capsuleCollider and characterController are mutually exclusive
            // Having both enabled would double fire triggers and other collisions
            characterController.enabled = true;
            this.enabled = true;
        }

        public override void OnStopAuthority()
        {
            this.enabled = false;

            // capsuleCollider and characterController are mutually exclusive
            // Having both enabled would double fire triggers and other collisions
            characterController.enabled = false;
        }

        public override void OnStartLocalPlayer()
        {
            if (ControllerUIPrefab != null)
                controllerUI = Instantiate(ControllerUIPrefab);

            if (controllerUI != null)
            {
                if (controllerUI.TryGetComponent(out TankControllerUI canvasControlPanel))
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
            HandleTurning(deltaTime);
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

        void HandleOptions()
        {
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

        void HandleMove(float deltaTime)
        {
            // Initialize target movement variables
            float targetMoveX = 0f;
            float targetMoveZ = 0f;

            // Check for WASD key presses and adjust target movement variables accordingly
            if (moveKeys.Forward != KeyCode.None && Input.GetKey(moveKeys.Forward)) targetMoveZ = 1f;
            if (moveKeys.Back != KeyCode.None && Input.GetKey(moveKeys.Back)) targetMoveZ = -1f;

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

            // Add gravity in case we drove off a cliff.
            direction += Physics.gravity;

            // Finally move the character.
            characterController.Move(direction * deltaTime);
        }
    }
}
