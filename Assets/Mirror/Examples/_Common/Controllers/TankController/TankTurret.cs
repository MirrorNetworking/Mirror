using System;
using UnityEngine;

namespace Mirror.Examples.Common.Controllers.Tank
{
    [AddComponentMenu("")]
    [RequireComponent(typeof(NetworkIdentity))]
    [RequireComponent(typeof(NetworkTransformReliable))]
    [RequireComponent(typeof(TankController))]
    [DisallowMultipleComponent]
    public class TankTurret : NetworkBehaviour
    {
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

        const float BASE_DPI = 96f;

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
        public GameObject TurretUIPrefab;
        public GameObject projectilePrefab;

        [Header("Components")]
        public Animator animator;
        public NetworkTransformReliable turretNTR;
        public NetworkTransformReliable barrelNTR;
        public Transform turret;
        public Transform barrel;
        public Transform projectileMount;

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

        [Header("Turret")]
        [Range(0, 300f)]
        [Tooltip("Max Rotation in degrees per second")]
        public float maxTurretSpeed = 250f;
        [Range(0, 10f)]
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

        [Header("Diagnostics")]
        [ReadOnly, SerializeField, Range(-1f, 1f)]
        float mouseInputX;
        [ReadOnly, SerializeField, Range(0, 30f)]
        float mouseSensitivity;
        [ReadOnly, SerializeField, Range(-300f, 300f)]
        float turretSpeed;
        [ReadOnly, SerializeField, Range(-180f, 180f)]
        float pitchAngle;
        [ReadOnly, SerializeField, Range(-180f, 180f)]
        float pitchSpeed;

        [ReadOnly, SerializeField]
        GameObject turretUI;

        void OnPlayerColorChanged(Color32 _, Color32 newColor)
        {
            if (cachedMaterial == null)
                cachedMaterial = playerObject.GetComponent<Renderer>().material;

            cachedMaterial.color = newColor;
            playerObject.SetActive(newColor != Color.black);
        }

        #region Unity Callbacks

        /// <summary>
        /// Add your validation code here after the base.OnValidate(); call.
        /// </summary>
        protected override void OnValidate()
        {
            // Skip if Editor is in Play mode
            if (Application.isPlaying) return;

            base.OnValidate();
            Reset();
        }

        // NOTE: Do not put objects in DontDestroyOnLoad (DDOL) in Awake.  You can do that in Start instead.
        void Reset()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            // Set default...this may be modified based on DPI at runtime
            mouseSensitivity = turretAcceleration;

            // Do a recursive search for a children named "Turret" and "ProjectileMount".
            // They will be several levels deep in the hierarchy.
            if (turret == null)
                turret = FindDeepChild(transform, "Turret");

            if (barrel == null)
                barrel = FindDeepChild(turret, "Barrel");

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

            // The base Tank uses the first NetworkTransformReliable for the tank body
            // Add additional NetworkTransformReliable components for the turret and barrel
            // Set SyncPosition to false because we only want to sync rotation
            NetworkTransformReliable[] NTRs = GetComponents<NetworkTransformReliable>();

            if (NTRs.Length < 2)
            {
                turretNTR = gameObject.AddComponent<NetworkTransformReliable>();
                turretNTR.transform.SetSiblingIndex(NTRs[0].transform.GetSiblingIndex() + 1);
                NTRs = GetComponents<NetworkTransformReliable>();
            }
            else
                turretNTR = NTRs[1];

            // Ensure SyncDirection is Client to Server
            turretNTR.syncDirection = SyncDirection.ClientToServer;
            turretNTR.syncPosition = false;
            turretNTR.compressRotation = false;

            // Set SyncPosition to false because we only want to sync rotation
            //turretNTR.syncPosition = false;

            if (turret != null)
                turretNTR.target = turret;

            if (NTRs.Length < 3)
            {
                barrelNTR = gameObject.AddComponent<NetworkTransformReliable>();
                barrelNTR.transform.SetSiblingIndex(NTRs[1].transform.GetSiblingIndex() + 1);
                NTRs = GetComponents<NetworkTransformReliable>();
            }
            else
                barrelNTR = NTRs[2];

            // Ensure SyncDirection is Client to Server
            barrelNTR.syncDirection = SyncDirection.ClientToServer;
            barrelNTR.syncPosition = false;
            barrelNTR.compressRotation = false;

            // Set SyncPosition to false because we only want to sync rotation
            //barrelNTR.syncPosition = false;

            if (barrel != null)
                barrelNTR.target = barrel;

#if UNITY_EDITOR
            // For convenience in the examples, we use the GUID of the Projectile prefab
            // to find the correct prefab in the Mirror/Examples/_Common/Controllers folder.
            // This avoids conflicts with user-created prefabs that may have the same name
            // and avoids polluting the user's project with Resources.
            // This is not recommended for production code...use Resources.Load or AssetBundles instead.
            if (TurretUIPrefab == null)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath("4d16730f7a8ba0a419530d1156d25080");
                TurretUIPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            if (projectilePrefab == null)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath("aec853915cd4f4477ba1532b5fe05488");
                projectilePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
#endif

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

                if (turretUI != null)
                    turretUI.SetActive(controlOptions.HasFlag(ControlOptions.ShowUI));
            }
        }

        void HandleTurning(float deltaTime)
        {
            float targetTurnSpeed = 0f;

            // Q and E cancel each other out, reducing targetTurnSpeed to zero.
            if (moveKeys.TurnLeft != KeyCode.None && Input.GetKey(moveKeys.TurnLeft))
                targetTurnSpeed -= maxTurretSpeed;
            if (moveKeys.TurnRight != KeyCode.None && Input.GetKey(moveKeys.TurnRight))
                targetTurnSpeed += maxTurretSpeed;

            turretSpeed = Mathf.MoveTowards(turretSpeed, targetTurnSpeed, turretAcceleration * maxTurretSpeed * deltaTime);
            turret.Rotate(0f, turretSpeed * deltaTime, 0f);
        }

        void HandleMouseTurret(float deltaTime)
        {
            // Accumulate mouse input over time
            mouseInputX += Input.GetAxisRaw("Mouse X") * mouseSensitivity;

            // Clamp the accumulator to simulate key press behavior
            mouseInputX = Mathf.Clamp(mouseInputX, -1f, 1f);

            // Calculate target turn speed
            float targetTurnSpeed = mouseInputX * maxTurretSpeed;

            // Use the same acceleration logic as HandleTurning
            turretSpeed = Mathf.MoveTowards(turretSpeed, targetTurnSpeed, mouseSensitivity * maxTurretSpeed * deltaTime);

            // Apply rotation
            turret.Rotate(0f, turretSpeed * deltaTime, 0f);

            // Decay the accumulator over time
            //float decayRate = 5f; // Adjust as needed
            //mouseInputX = Mathf.MoveTowards(mouseInputX, 0f, decayRate * deltaTime);
            mouseInputX = Mathf.MoveTowards(mouseInputX, 0f, mouseSensitivity * deltaTime);
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

            pitchSpeed = Mathf.MoveTowards(pitchSpeed, targetPitchSpeed, pitchAcceleration * maxPitchSpeed * deltaTime);

            // Apply pitch rotation
            pitchAngle += pitchSpeed * deltaTime;
            pitchAngle = Mathf.Clamp(pitchAngle, -maxPitchUpAngle, maxPitchDownAngle);

            // Return to -90 when no input
            if (!inputDetected && controlOptions.HasFlag(ControlOptions.AutoLevel))
                pitchAngle = Mathf.MoveTowards(pitchAngle, 0f, maxPitchSpeed * deltaTime);

            // Apply rotation to barrel -- rotation is (-90, 0, 180) in the prefab
            // so that's what we have to work towards.
            barrel.localRotation = Quaternion.Euler(-90f + pitchAngle, 0f, 180f);
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

        // This has multiple callers in different contexts...don't consolidate, even if it looks like you could.
        void DoShoot()
        {
            //Debug.Log($"DoShoot isServerOnly:{isServerOnly} | isServer:{isServer} | isClient:{isClient}");

            // Turret
            // - Barrel (with Collider)
            //   - BarrelEnd
            //     - ProjectileMount
            // projectileMount.transform.parent.parent is the Barrel object with the Collider

            if (isServerOnly)
            {
                // Dedicated Server logic - no host client
                GameObject go = Instantiate(projectilePrefab, projectileMount.position, projectileMount.rotation);
                Physics.IgnoreCollision(go.GetComponent<Collider>(), projectileMount.transform.parent.parent.GetComponent<Collider>());
            }
            else if (isServer)
            {
                // Server logic, including host client
                //animator.SetTrigger("Shoot");
                GameObject go = Instantiate(projectilePrefab, projectileMount.position, projectileMount.rotation);
                Physics.IgnoreCollision(go.GetComponent<Collider>(), projectileMount.transform.parent.parent.GetComponent<Collider>());
            }

            if (isClientOnly)
            {
                // Client-side logic, excluding host client
                //animator.SetTrigger("Shoot");
                GameObject go = Instantiate(projectilePrefab, projectileMount.position, projectileMount.rotation);
                Physics.IgnoreCollision(go.GetComponent<Collider>(), projectileMount.transform.parent.parent.GetComponent<Collider>());
            }
        }

        #endregion

        #region Start & Stop Callbacks

        public override void OnStartServer() { }

        public override void OnStartLocalPlayer()
        {
            if (TurretUIPrefab != null)
                turretUI = Instantiate(TurretUIPrefab);

            if (turretUI != null)
            {
                if (turretUI.TryGetComponent(out TurretUI canvasControlPanel))
                    canvasControlPanel.Refresh(moveKeys, optionsKeys);

                turretUI.SetActive(controlOptions.HasFlag(ControlOptions.ShowUI));
            }
        }

        public override void OnStopLocalPlayer()
        {
            if (turretUI != null)
                Destroy(turretUI);
            turretUI = null;
        }

        public override void OnStartAuthority()
        {
            // Calculate DPI-aware sensitivity
            float dpiScale = (Screen.dpi > 0) ? (Screen.dpi / BASE_DPI) : 1f;
            mouseSensitivity = turretAcceleration * dpiScale;
            //Debug.Log($"Screen DPI: {Screen.dpi}, DPI Scale: {dpiScale}, Adjusted Turn Acceleration: {turnAccelerationDPI}");

            SetCursor(controlOptions.HasFlag(ControlOptions.MouseLock));
            this.enabled = true;
        }

        public override void OnStopAuthority()
        {
            SetCursor(false);
            this.enabled = false;
        }

        #endregion
    }
}
