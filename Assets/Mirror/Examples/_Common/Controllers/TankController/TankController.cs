using System;
using UnityEngine;
using UnityEngine.Serialization;
using Mirror;

namespace Mirror.Examples.Common.Controllers.Tank
{
    [Serializable]
    public struct MoveKeys
    {
        public KeyCode Forward;
        public KeyCode Back;
        public KeyCode TurnLeft;
        public KeyCode TurnRight;
    }

    [Serializable]
    public struct OtherKeys
    {
        public KeyCode Shoot;
    }

    [Serializable]
    public struct OptionsKeys
    {
        public KeyCode MouseLock;
        public KeyCode AutoRun;
        public KeyCode ToggleUI;
    }

    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(PlayerCamera))]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(NetworkTransformReliable))]
    [DisallowMultipleComponent]
    public class TankController : NetworkBehaviour
    {
        public enum GroundState : byte { Jumping, Falling, Grounded }

        [Flags]
        public enum ControlOptions : byte
        {
            None,
            MouseLock = 1 << 0,
            AutoRun = 1 << 1,
            ShowUI = 1 << 2
        }

        [Header("Avatar Components")]
        public BoxCollider boxCollider;
        public Animator animator;
        public CharacterController characterController;
        public NetworkTransformReliable tankNTR;
        public NetworkTransformReliable turretNTR;

        [Header("Turret")]
        public Transform turret;
        public Transform projectileMount;
        public GameObject projectilePrefab;

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
        public OtherKeys otherKeys = new OtherKeys
        {
            Shoot = KeyCode.Space
        };

        [SerializeField]
        public OptionsKeys optionsKeys = new OptionsKeys
        {
            MouseLock = KeyCode.M,
            AutoRun = KeyCode.R,
            ToggleUI = KeyCode.U
        };

        [Space(5)]
        public ControlOptions controlOptions = ControlOptions.MouseLock | ControlOptions.ShowUI;

        [Header("Movement")]
        [Range(0, 20)]
        [FormerlySerializedAs("moveSpeedMultiplier")]
        [Tooltip("Speed in meters per second")]
        public float maxMoveSpeed = 4f;

        // Replacement for Sensitvity from Input Settings.
        [Range(0, 10f)]
        [Tooltip("Sensitivity factors into accelleration")]
        public float inputSensitivity = 2f;

        // Replacement for Gravity from Input Settings.
        [Range(0, 10f)]
        [Tooltip("Gravity factors into decelleration")]
        public float inputGravity = 2f;

        [Header("Turning")]
        [Range(0, 180f)]
        [Tooltip("Max Rotation in degrees per second")]
        public float maxTurnSpeed = 100f;
        [Range(0, 5f)]
        [FormerlySerializedAs("turnDelta")]
        [Tooltip("Rotation acceleration in degrees per second squared")]
        public float turnAcceleration = 3f;

        [Header("Turret (Mouse)")]
        [Range(0, 360f)]
        [Tooltip("Max Rotation in degrees per second")]
        public float maxTurretSpeed = 180f;
        [Range(0, 1f)]
        [Tooltip("Sensitivity factors into accelleration")]
        public float mouseSensitivity = 0.01f;
        [Range(0, 0.5f)]
        [Tooltip("Time to reach the target rotation")]
        public float smoothTime = 0.2f;

        [Header("Diagnostics")]
        [ReadOnly, SerializeField]
        GroundState groundState = GroundState.Grounded;

        [ReadOnly, SerializeField, Range(-1f, 1f)]
        float horizontal;
        [ReadOnly, SerializeField, Range(-1f, 1f)]
        float vertical;

        [ReadOnly, SerializeField, Range(-180f, 180f)]
        float turnSpeed;
        [ReadOnly, SerializeField, Range(-1000f, 1000f)]
        private float turnSmoothSpeed;

        [ReadOnly, SerializeField, Range(-360f, 360f)]
        float turretSpeed;

        [ReadOnly, SerializeField, Range(-1.5f, 1.5f)]
        float animVelocity;

        [ReadOnly, SerializeField, Range(-1.5f, 1.5f)]
        float animRotation;

        [ReadOnly, SerializeField]
        Vector3 direction;

        [ReadOnly, SerializeField]
        Vector3Int velocity;

        [ReadOnly, SerializeField]
        Vector3 mouseDelta;

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
            if (boxCollider == null)
                boxCollider = GetComponent<BoxCollider>();

            if (animator == null)
                animator = GetComponent<Animator>();

            boxCollider.center = new Vector3(0, 0.65f, 0.75f);
            boxCollider.size = new Vector3(4.5f, 3.5f, 9f);

            // Enable by default...it will be disabled when characterController is enabled
            boxCollider.enabled = true;

            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            // Override CharacterController default values
            characterController.enabled = false;
            characterController.skinWidth = 0.02f;
            characterController.minMoveDistance = 0f;

            GetComponent<Rigidbody>().isKinematic = true;

            if (turret == null)
                turret = FindDeepChild(transform, "Turret");

            if (projectileMount == null)
                projectileMount = FindDeepChild(turret, "ProjectileMount");

            // do a recursive search for a child named "Turret"
            // it will be several levels deep in the hierarchy
            // tranform.Find will fail - must do recursive search
            // FindDeepChild doesn't exist in Unity so you have to write it.
            Transform FindDeepChild(Transform aParent, string aName)
            {
                var result = aParent.Find(aName);
                if (result != null)
                    return result;

                foreach (Transform child in aParent)
                {
                    result = FindDeepChild(child, aName);
                    if (result != null)
                        return result;
                }

                return null;
            }

            NetworkTransformReliable[] NTRs = GetComponents<NetworkTransformReliable>();
            if (NTRs.Length > 0)
            {
                tankNTR = NTRs[0];
                tankNTR.target = transform;
            }

            if (NTRs.Length < 2)
            {
                gameObject.AddComponent<NetworkTransformReliable>();
                NTRs = GetComponents<NetworkTransformReliable>();
            }

            if (NTRs.Length > 1)
            {
                turretNTR = NTRs[1];
                if (turret != null)
                    turretNTR.target = turret;
            }

#if UNITY_EDITOR
            // For convenience in the examples, we use the GUID of the PlayerControllerUI and Projectile
            // to find the correct prefabs in the Mirror/Examples/_Common/Controllers folder.
            // This avoids conflicts with user-created prefabs that may have the same name
            // and avoids polluting the user's project with Resources.
            // This is not recommended for production code...use Resources.Load or AssetBundles instead.
            if (ControllerUIPrefab == null)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath("e64b14552402f6745a7f0aca6237fae2");
                ControllerUIPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            if (projectilePrefab == null)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath("aec853915cd4f4477ba1532b5fe05488");
                projectilePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
#endif

            this.enabled = false;
        }

        public override void OnStartAuthority()
        {
            SetCursor(controlOptions.HasFlag(ControlOptions.MouseLock));

            // capsuleCollider and characterController are mutually exclusive
            // Having both enabled would double fire triggers and other collisions
            //boxCollider.enabled = false;
            characterController.enabled = true;
            this.enabled = true;
        }

        public override void OnStopAuthority()
        {
            this.enabled = false;

            // capsuleCollider and characterController are mutually exclusive
            // Having both enabled would double fire triggers and other collisions
            //boxCollider.enabled = true;
            characterController.enabled = false;

            SetCursor(false);
        }

        public override void OnStartLocalPlayer()
        {
            if (ControllerUIPrefab != null)
                controllerUI = Instantiate(ControllerUIPrefab);

            if (controllerUI != null)
            {
                if (controllerUI.TryGetComponent<TankControllerUI>(out TankControllerUI canvasControlPanel))
                    canvasControlPanel.Refresh(moveKeys, otherKeys, optionsKeys);

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
            HandleShooting();

            if (controlOptions.HasFlag(ControlOptions.MouseLock))
                HandleMouseTurret(deltaTime);

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

        void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void HandleOptions()
        {
            if (optionsKeys.MouseLock != KeyCode.None && Input.GetKeyUp(optionsKeys.MouseLock))
            {
                controlOptions ^= ControlOptions.MouseLock;
                SetCursor(controlOptions.HasFlag(ControlOptions.MouseLock));
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

        #region Shooting

        void HandleShooting()
        {
            if (otherKeys.Shoot != KeyCode.None && Input.GetKeyUp(otherKeys.Shoot))
            {
                CmdShoot();
                if (!isServer) DoShoot();
            }
        }

        [Command]
        void CmdShoot()
        {
            //Debug.Log("CmdShoot");
            RpcShoot();
            DoShoot();
        }

        [ClientRpc(includeOwner = false)]
        void RpcShoot()
        {
            //Debug.Log("RpcShoot");
            if (!isServer) DoShoot();
        }

        void DoShoot()
        {
            //Debug.Log($"DoShoot {isServerOnly} {isClient}");
            if (isServerOnly)
            {
                // Dedicated Server logic - no host client
                Instantiate(projectilePrefab, projectileMount.position, projectileMount.rotation);
            }
            else if (isServer)
            {
                // Server logic, including host client
                animator.SetTrigger("Shoot");
                Instantiate(projectilePrefab, projectileMount.position, projectileMount.rotation);
            }

            if (isClientOnly)
            {
                // Client-side logic, excluding host client
                animator.SetTrigger("Shoot");
                Instantiate(projectilePrefab, projectileMount.position, projectileMount.rotation);
            }
        }

        #endregion

        void HandleMouseTurret(float deltaTime)
        {
            // Use GetAxisRaw for more responsive input and clamp
            float mouseX = Mathf.Clamp(Input.GetAxisRaw("Mouse X"), -1f, 1f);

            // Calculate target turn speed with sensitivity
            float targetTurnSpeed = mouseX * maxTurnSpeed * mouseSensitivity;
            targetTurnSpeed = Mathf.Clamp(targetTurnSpeed, -maxTurretSpeed, maxTurretSpeed);

            // Smoothly interpolate the turn speed
            turretSpeed = Mathf.SmoothDamp(turretSpeed, targetTurnSpeed, ref turnSmoothSpeed, smoothTime);

            // Apply rotation
            turret.Rotate(Vector3.up * turretSpeed, Space.World);
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

            // Finally move the character.
            characterController.Move(direction * deltaTime);
        }
    }
}
