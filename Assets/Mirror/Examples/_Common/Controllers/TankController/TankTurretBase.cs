using System;
using UnityEngine;

namespace Mirror.Examples.Common.Controllers.Tank
{
    [AddComponentMenu("")]
    [RequireComponent(typeof(NetworkIdentity))]
    [DisallowMultipleComponent]
    public class TankTurretBase : NetworkBehaviour
    {
        const float BASE_DPI = 96f;

        [Serializable]
        public struct OptionsKeys
        {
            public KeyCode MouseLock;
            public KeyCode AutoLevel;
            public KeyCode ToggleUI;
        }

        [Serializable]
        public struct MoveKeys
        {
            public KeyCode PitchUp;
            public KeyCode PitchDown;
            public KeyCode TurnLeft;
            public KeyCode TurnRight;
        }

        [Serializable]
        public struct OtherKeys
        {
            public KeyCode Shoot;
        }

        [Flags]
        public enum ControlOptions : byte
        {
            None,
            MouseLock = 1 << 0,
            AutoLevel = 1 << 1,
            ShowUI = 1 << 2
        }

        // Unity clones the material when GetComponent<Renderer>().material is called
        // Cache it here and destroy it in OnDestroy to prevent a memory leak
        Material cachedMaterial;

        [Header("Prefabs")]
        public GameObject turretUIPrefab;
        public GameObject projectilePrefab;

        [Header("Components")]
        public Animator animator;
        public Transform turret;
        public Transform barrel;
        public Transform projectileMount;
        public CapsuleCollider barrelCollider;

        [Header("Seated Player")]
        public GameObject playerObject;

        [SyncVar(hook = nameof(OnPlayerColorChanged))]
        public Color32 playerColor = Color.black;

        [Header("Configuration")]
        [SerializeField]
        public MoveKeys moveKeys = new MoveKeys
        {
            PitchUp = KeyCode.UpArrow,
            PitchDown = KeyCode.DownArrow,
            TurnLeft = KeyCode.LeftArrow,
            TurnRight = KeyCode.RightArrow
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
            AutoLevel = KeyCode.L,
            ToggleUI = KeyCode.U
        };

        [Space(5)]
        public ControlOptions controlOptions = ControlOptions.AutoLevel | ControlOptions.ShowUI;

        [Header("Shooting")]
        [Tooltip("Cooldown time in seconds")]
        [Range(0, 10)]
        public byte cooldownTime = 1;

        [Header("Turret")]
        [Range(0, 300f)]
        [Tooltip("Max Rotation in degrees per second")]
        public float maxTurretSpeed = 250f;
        [Range(0, 30f)]
        [Tooltip("Rotation acceleration in degrees per second squared")]
        public float turretAcceleration = 10f;

        [Header("Barrel")]
        [Range(0, 180f)]
        [Tooltip("Max Pitch in degrees per second")]
        public float maxPitchSpeed = 30f;
        [Range(0, 40f)]
        [Tooltip("Max Pitch in degrees")]
        public float maxPitchUpAngle = 25f;
        [Range(0, 20f)]
        [Tooltip("Max Pitch in degrees")]
        public float maxPitchDownAngle = 0f;
        [Range(0, 10f)]
        [Tooltip("Pitch acceleration in degrees per second squared")]
        public float pitchAcceleration = 3f;

        // Runtime data in a struct so it can be folded up in inspector
        [Serializable]
        public struct RuntimeData
        {
            [ReadOnly, SerializeField, Range(-300f, 300f)] float _turretSpeed;
            [ReadOnly, SerializeField, Range(-180f, 180f)] float _pitchAngle;
            [ReadOnly, SerializeField, Range(-180f, 180f)] float _pitchSpeed;
            [ReadOnly, SerializeField, Range(-1f, 1f)] float _mouseInputX;
            [ReadOnly, SerializeField, Range(0, 30f)] float _mouseSensitivity;
            [ReadOnly, SerializeField] double _lastShotTime;
            [ReadOnly, SerializeField] GameObject _turretUI;

            #region Properties

            public float mouseInputX
            {
                get => _mouseInputX;
                internal set => _mouseInputX = value;
            }

            public float mouseSensitivity
            {
                get => _mouseSensitivity;
                internal set => _mouseSensitivity = value;
            }

            public float turretSpeed
            {
                get => _turretSpeed;
                internal set => _turretSpeed = value;
            }

            public float pitchAngle
            {
                get => _pitchAngle;
                internal set => _pitchAngle = value;
            }

            public float pitchSpeed
            {
                get => _pitchSpeed;
                internal set => _pitchSpeed = value;
            }

            public double lastShotTime
            {
                get => _lastShotTime;
                internal set => _lastShotTime = value;
            }

            public GameObject turretUI
            {
                get => _turretUI;
                internal set => _turretUI = value;
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

        // NOTE: Do not put objects in DontDestroyOnLoad (DDOL) in Awake.  You can do that in Start instead.
        protected virtual void Reset()
        {
            // Ensure syncDirection is Client to Server
            syncDirection = SyncDirection.ClientToServer;

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            // Set default...this may be modified based on DPI at runtime
            runtimeData.mouseSensitivity = turretAcceleration;

            // Do a recursive search for a children named "Turret" and "ProjectileMount".
            // They will be several levels deep in the hierarchy.
            if (turret == null)
                turret = FindDeepChild(transform, "Turret");

            if (barrel == null)
                barrel = FindDeepChild(turret, "Barrel");

            if (barrelCollider == null)
                barrelCollider = barrel.GetComponent<CapsuleCollider>();

            if (projectileMount == null)
                projectileMount = FindDeepChild(turret, "ProjectileMount");

            if (playerObject == null)
                playerObject = FindDeepChild(turret, "SeatedPlayer").gameObject;

            // tranform.Find will fail - must do recursive search
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

#if UNITY_EDITOR
            // For convenience in the examples, we use the GUID of the Projectile prefab
            // to find the correct prefab in the Mirror/Examples/_Common/Controllers folder.
            // This avoids conflicts with user-created prefabs that may have the same name
            // and avoids polluting the user's project with Resources.
            // This is not recommended for production code...use Resources.Load or AssetBundles instead.
            if (turretUIPrefab == null)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath("4d16730f7a8ba0a419530d1156d25080");
                turretUIPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            if (projectilePrefab == null)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath("aec853915cd4f4477ba1532b5fe05488");
                projectilePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
#endif

            this.enabled = false;
        }

        public override void OnStartLocalPlayer()
        {
            if (turretUIPrefab != null)
                runtimeData.turretUI = Instantiate(turretUIPrefab);

            if (runtimeData.turretUI != null)
            {
                if (runtimeData.turretUI.TryGetComponent(out TurretUI canvasControlPanel))
                    canvasControlPanel.Refresh(moveKeys, optionsKeys);

                runtimeData.turretUI.SetActive(controlOptions.HasFlag(ControlOptions.ShowUI));
            }
        }

        public override void OnStopLocalPlayer()
        {
            if (runtimeData.turretUI != null)
                Destroy(runtimeData.turretUI);
            runtimeData.turretUI = null;
        }

        public override void OnStartAuthority()
        {
            // Calculate DPI-aware sensitivity
            float dpiScale = (Screen.dpi > 0) ? (Screen.dpi / BASE_DPI) : 1f;
            runtimeData.mouseSensitivity = turretAcceleration * dpiScale;

            SetCursor(controlOptions.HasFlag(ControlOptions.MouseLock));
            this.enabled = true;
        }

        public override void OnStopAuthority()
        {
            SetCursor(false);
            this.enabled = false;
        }

        #endregion

        void Update()
        {
            float deltaTime = Time.deltaTime;

            HandleOptions();
            HandlePitch(deltaTime);

            if (controlOptions.HasFlag(ControlOptions.MouseLock))
                HandleMouseTurret(deltaTime);
            else
                HandleTurning(deltaTime);

            HandleShooting();
        }

        void OnPlayerColorChanged(Color32 _, Color32 newColor)
        {
            if (cachedMaterial == null)
                cachedMaterial = playerObject.GetComponent<Renderer>().material;

            cachedMaterial.color = newColor;
            playerObject.SetActive(newColor != Color.black);
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

            if (optionsKeys.AutoLevel != KeyCode.None && Input.GetKeyUp(optionsKeys.AutoLevel))
                controlOptions ^= ControlOptions.AutoLevel;

            if (optionsKeys.ToggleUI != KeyCode.None && Input.GetKeyUp(optionsKeys.ToggleUI))
            {
                controlOptions ^= ControlOptions.ShowUI;

                if (runtimeData.turretUI != null)
                    runtimeData.turretUI.SetActive(controlOptions.HasFlag(ControlOptions.ShowUI));
            }
        }

        void HandleTurning(float deltaTime)
        {
            float targetTurnSpeed = 0f;

            // TurnLeft and TurnRight cancel each other out, reducing targetTurnSpeed to zero.
            if (moveKeys.TurnLeft != KeyCode.None && Input.GetKey(moveKeys.TurnLeft))
                targetTurnSpeed -= maxTurretSpeed;
            if (moveKeys.TurnRight != KeyCode.None && Input.GetKey(moveKeys.TurnRight))
                targetTurnSpeed += maxTurretSpeed;

            runtimeData.turretSpeed = Mathf.MoveTowards(runtimeData.turretSpeed, targetTurnSpeed, turretAcceleration * maxTurretSpeed * deltaTime);
            turret.Rotate(0f, runtimeData.turretSpeed * deltaTime, 0f);
        }

        void HandleMouseTurret(float deltaTime)
        {
            // Accumulate mouse input over time
            runtimeData.mouseInputX += Input.GetAxisRaw("Mouse X") * runtimeData.mouseSensitivity;

            // Clamp the accumulator to simulate key press behavior
            runtimeData.mouseInputX = Mathf.Clamp(runtimeData.mouseInputX, -1f, 1f);

            // Calculate target turn speed
            float targetTurnSpeed = runtimeData.mouseInputX * maxTurretSpeed;

            // Use the same acceleration logic as HandleTurning
            runtimeData.turretSpeed = Mathf.MoveTowards(runtimeData.turretSpeed, targetTurnSpeed, runtimeData.mouseSensitivity * maxTurretSpeed * deltaTime);

            // Apply rotation
            turret.Rotate(0f, runtimeData.turretSpeed * deltaTime, 0f);

            runtimeData.mouseInputX = Mathf.MoveTowards(runtimeData.mouseInputX, 0f, runtimeData.mouseSensitivity * deltaTime);
        }

        void HandlePitch(float deltaTime)
        {
            float targetPitchSpeed = 0f;
            bool inputDetected = false;

            // Up and Down arrows for pitch
            if (moveKeys.PitchUp != KeyCode.None && Input.GetKey(moveKeys.PitchUp))
            {
                targetPitchSpeed -= maxPitchSpeed;
                inputDetected = true;
            }

            if (moveKeys.PitchDown != KeyCode.None && Input.GetKey(moveKeys.PitchDown))
            {
                targetPitchSpeed += maxPitchSpeed;
                inputDetected = true;
            }

            runtimeData.pitchSpeed = Mathf.MoveTowards(runtimeData.pitchSpeed, targetPitchSpeed, pitchAcceleration * maxPitchSpeed * deltaTime);

            // Apply pitch rotation
            runtimeData.pitchAngle += runtimeData.pitchSpeed * deltaTime;
            runtimeData.pitchAngle = Mathf.Clamp(runtimeData.pitchAngle, -maxPitchUpAngle, maxPitchDownAngle);

            // Return to -90 when no input
            if (!inputDetected && controlOptions.HasFlag(ControlOptions.AutoLevel))
                runtimeData.pitchAngle = Mathf.MoveTowards(runtimeData.pitchAngle, 0f, maxPitchSpeed * deltaTime);

            // Apply rotation to barrel -- rotation is (-90, 0, 180) in the prefab
            // so that's what we have to work towards.
            barrel.localRotation = Quaternion.Euler(-90f + runtimeData.pitchAngle, 0f, 180f);
        }

        #region Shooting

        bool CanShoot => NetworkTime.time >= runtimeData.lastShotTime + cooldownTime;

        void HandleShooting()
        {
            if (CanShoot && otherKeys.Shoot != KeyCode.None && Input.GetKeyUp(otherKeys.Shoot))
            {
                CmdShoot();
                if (!isServer) DoShoot();
            }
        }

        [Command]
        void CmdShoot()
        {
            if (!CanShoot) return;

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
            //Debug.Log($"DoShoot isServerOnly:{isServerOnly} | isServer:{isServer} | isClientOnly:{isClientOnly}");

            // Turret
            // - Barrel (with Collider)
            //   - BarrelEnd
            //     - ProjectileMount

            // Locally instantiate the projectile at the end of the barrel
            GameObject go = Instantiate(projectilePrefab, projectileMount.position, projectileMount.rotation);

            // Ignore collision between the projectile and the barrel collider
            Physics.IgnoreCollision(go.GetComponent<Collider>(), barrelCollider);

            // Update the last shot time
            runtimeData.lastShotTime = NetworkTime.time;
        }

        #endregion
    }
}
