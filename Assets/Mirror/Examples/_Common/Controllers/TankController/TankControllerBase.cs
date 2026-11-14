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
        public enum GroundState : byte { Grounded, Falling }

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

        // Runtime data in a struct so it can be folded up in inspector
        [Serializable]
        public struct RuntimeData
        {
            [ReadOnly, SerializeField, Range(-1f, 1f)] float _vertical;
            [ReadOnly, SerializeField, Range(-300f, 300f)] float _turnSpeed;
            [ReadOnly, SerializeField, Range(-1.5f, 1.5f)] float _animVelocity;
            [ReadOnly, SerializeField, Range(-1.5f, 1.5f)] float _animRotation;
            [ReadOnly, SerializeField] GroundState _groundState;
            [ReadOnly, SerializeField] Vector3 _direction;
            [ReadOnly, SerializeField] Vector3Int _velocity;
            [ReadOnly, SerializeField] GameObject _controllerUI;

            #region Properties

            public float vertical
            {
                get => _vertical;
                internal set => _vertical = value;
            }

            public float turnSpeed
            {
                get => _turnSpeed;
                internal set => _turnSpeed = value;
            }

            public float animVelocity
            {
                get => _animVelocity;
                internal set => _animVelocity = value;
            }

            public float animRotation
            {
                get => _animRotation;
                internal set => _animRotation = value;
            }

            public GameObject controllerUI
            {
                get => _controllerUI;
                internal set => _controllerUI = value;
            }

            public Vector3 direction
            {
                get => _direction;
                internal set => _direction = value;
            }

            public Vector3Int velocity
            {
                get => _velocity;
                internal set => _velocity = value;
            }

            public GroundState groundState
            {
                get => _groundState;
                internal set => _groundState = value;
            }

            #endregion
        }

        [Header("Diagnostics")]
        public RuntimeData runtimeData;

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
            runtimeData.vertical = 0f;
            runtimeData.turnSpeed = 0f;
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

        public override void OnStartLocalPlayer()
        {
            if (ControllerUIPrefab != null)
                runtimeData.controllerUI = Instantiate(ControllerUIPrefab);

            if (runtimeData.controllerUI != null)
            {
                if (runtimeData.controllerUI.TryGetComponent(out TankControllerUI canvasControlPanel))
                    canvasControlPanel.Refresh(moveKeys, optionsKeys);

                runtimeData.controllerUI.SetActive(controlOptions.HasFlag(ControlOptions.ShowUI));
            }
        }

        public override void OnStopLocalPlayer()
        {
            if (runtimeData.controllerUI != null)
                Destroy(runtimeData.controllerUI);
            runtimeData.controllerUI = null;
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
            runtimeData.groundState = characterController.isGrounded ? GroundState.Grounded : GroundState.Falling;

            // Diagnostic velocity...FloorToInt for display purposes
            runtimeData.velocity = Vector3Int.FloorToInt(characterController.velocity);
        }

        void HandleOptions()
        {
            if (optionsKeys.AutoRun != KeyCode.None && Input.GetKeyUp(optionsKeys.AutoRun))
                controlOptions ^= ControlOptions.AutoRun;

            if (optionsKeys.ToggleUI != KeyCode.None && Input.GetKeyUp(optionsKeys.ToggleUI))
            {
                controlOptions ^= ControlOptions.ShowUI;

                if (runtimeData.controllerUI != null)
                    runtimeData.controllerUI.SetActive(controlOptions.HasFlag(ControlOptions.ShowUI));
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
                runtimeData.turnSpeed = Mathf.MoveTowards(runtimeData.turnSpeed, targetTurnSpeed, turnAcceleration * maxTurnSpeed * deltaTime);

            transform.Rotate(0f, runtimeData.turnSpeed * deltaTime, 0f);
        }

        void HandleMove(float deltaTime)
        {
            // Initialize target movement variables
            float targetMoveZ = 0f;

            // Check for WASD key presses and adjust target movement variables accordingly
            if (moveKeys.Forward != KeyCode.None && Input.GetKey(moveKeys.Forward)) targetMoveZ = 1f;
            if (moveKeys.Back != KeyCode.None && Input.GetKey(moveKeys.Back)) targetMoveZ = -1f;

            if (targetMoveZ == 0f)
            {
                if (!controlOptions.HasFlag(ControlOptions.AutoRun))
                    runtimeData.vertical = Mathf.MoveTowards(runtimeData.vertical, targetMoveZ, inputGravity * deltaTime);
            }
            else
                runtimeData.vertical = Mathf.MoveTowards(runtimeData.vertical, targetMoveZ, inputSensitivity * deltaTime);
        }

        void ApplyMove(float deltaTime)
        {
            // Create initial direction vector (z-axis only)
            runtimeData.direction = new Vector3(0f, 0f, runtimeData.vertical);

            // Transforms direction from local space to world space.
            runtimeData.direction = transform.TransformDirection(runtimeData.direction);

            // Multiply for desired ground speed.
            runtimeData.direction *= maxMoveSpeed;

            // Add gravity in case we drove off a cliff.
            runtimeData.direction += Physics.gravity;

            // Finally move the character.
            characterController.Move(runtimeData.direction * deltaTime);
        }
    }
}
