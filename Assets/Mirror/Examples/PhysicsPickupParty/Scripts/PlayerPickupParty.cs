using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace Mirror.Examples.PhysicsPickupParty
{
    public class PlayerPickupParty : NetworkBehaviour
    {

        public float moveSpeed = 8f;
        public float jumpSpeed = 8f;
        public float gravity = 20f;

        private Vector3 movement = Vector3.zero;
        private float verticalSpeed = 0f;
        public float armRotationSpeed = 25f;
        public bool canPickup = false;
        public Transform[] armPivots; // acts like inventory slots
        public PlayerArmSlot[] playerArmSlots; // acts like inventory slots
        public bool freezeRotationRB = true; // if false, picked up objects rotate with collision

        public CameraFollow cameraFollow;
        public CharacterController characterController;
        private Camera mainCamera;
        private int slotActive = 0; // 0 right arm, 1 left arm
        private NetworkIdentity pickedUpNetworkIdentity;
        private SceneReference sceneReference;
        public Renderer[] playerRenderers;
        public Material materialTeamColour;

        public AudioSource[] jumpSounds;
        public AudioSource pickupSound, dropSound, Arm1Sound, Arm2Sound;

        [SyncVar(hook = nameof(OnTeamChanged))]
        public int teamID = -1; // use -1 so 0 triggers hook

        void Awake()
        {
            // Allow all players to run this, they may need it for reference
#if UNITY_2022_2_OR_NEWER
            sceneReference = GameObject.FindAnyObjectByType<SceneReference>();
#else
            sceneReference = GameObject.FindObjectOfType<SceneReference>();
#endif
        }

#if !UNITY_SERVER
        public override void OnStartLocalPlayer()
        {
            sceneReference.playerPickupParty = this;
            // Grab and setup camera for local player only
            mainCamera = Camera.main;
            cameraFollow = mainCamera.GetComponent<CameraFollow>();
            cameraFollow.targetTransform = this.transform;

            // a check for late joiners, hooks get called before references are set
            // so we need this here
            if (sceneReference.teamManager.gameStatus == 1)
            {
                cameraFollow.enabled = true;
            }
        }
#endif

        public override void OnStartServer()
        {
            sceneReference.teamManager.AddPlayerTeamList(this);
        }

        public override void OnStopServer()
        {
            sceneReference.teamManager.RemovePlayerTeamList(teamID);
        }

        public void OnTeamChanged(int _old, int _new)
        {
            //print("OnTeamChanged: " + teamID);
            if (isOwned)
            {
                sceneReference.SetUIBGTeamColour(teamID);
                sceneReference.teamManager.SetPlayerSpawnPoint(teamID, this);
            }

            materialTeamColour = playerRenderers[0].material;
            materialTeamColour.color = sceneReference.teamManager.teamColours[teamID];
            foreach (Renderer renderer in playerRenderers)
            {
                renderer.material = materialTeamColour;
            }
        }

#if !UNITY_SERVER
        [ClientCallback]
        void Update()
        {
            //foreach (PlayerArmSlot obj in playerArmSlots)
            //{
            //    obj.pickedUpNetworkObject.position = 
            //}
            //playerArmSlots[slotActive].pickedUpNetworkObject

            if (!Application.isFocused) return;
            if (isOwned == false) { return; }

            if (sceneReference == null) { return; }
            
            // allow rotation and arm movement always, as its fun to do whilst waiting/ending
            RotatePlayerToMouse();
            RotateArmsToMouse();

            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Q))
            {
                if (slotActive == 0)
                {
                    slotActive = 1;
                    PlayAudio(3);
                    CmdPlayAudio(3);
                }
                else
                {
                    slotActive = 0;
                    PlayAudio(4);
                    CmdPlayAudio(4);
                }
            }

            // make sure our game mode is in "play" status.
            if (sceneReference.teamManager.gameStatus == 1)
            {
                PlayerMovement();

                if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E))
                {
                    Interact();
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

            //Vector3 horizontalMovement = new Vector3(moveHorizontal, 0f, moveVertical);
            //if (horizontalMovement.magnitude > 1f) horizontalMovement.Normalize();
            //horizontalMovement *= moveSpeed;

            Vector3 cameraForward = Camera.main.transform.forward;
            Vector3 cameraRight = Camera.main.transform.right;
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();
            Vector3 horizontalMovement = (cameraForward * moveVertical + cameraRight * moveHorizontal).normalized * moveSpeed;

            if (characterController.isGrounded)
            {
                verticalSpeed = -gravity * Time.deltaTime;
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    verticalSpeed = jumpSpeed;
                    PlayAudio(5);
                    CmdPlayAudio(5);
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
        void Interact()
        {
            if (canPickup && playerArmSlots[slotActive].pickedUpNetworkObject == null && playerArmSlots[slotActive].pickupObject != null)
            {
                playerArmSlots[slotActive].CmdPickup(playerArmSlots[slotActive].pickupObject.GetComponent<NetworkIdentity>());
                canPickup = false;
                PlayAudio(1);
                CmdPlayAudio(1);
            }
            else if (playerArmSlots[slotActive].pickedUpNetworkObject != null)
            {
                playerArmSlots[slotActive].CmdDrop();
                PlayAudio(2);
                CmdPlayAudio(2);
            }
        }
#endif

        public void OnDestroy()
        {
            if (cameraFollow)
            {
                // reset camera, useful for testing without stopping editor, or using offline scenes.
                cameraFollow.transform.position = new Vector3(0, 50, 0);
                cameraFollow.transform.localEulerAngles = new Vector3(90, 0, 0);
            }
        }

        [Command]
        public void CmdPlayAudio(int _value)
        {
            RpcPlayAudio(_value);
        }

        [ClientRpc(includeOwner = false)]
        public void RpcPlayAudio(int _value)
        {
            PlayAudio(_value);
        }

        public void PlayAudio(int _value)
        {
            if (_value == 1)
            {
                pickupSound.Play();
            }
            else if (_value == 2)
            {
                dropSound.Play();
            }
            else if (_value == 3)
            {
                Arm1Sound.Play();
            }
            else if (_value == 4)
            {
                Arm2Sound.Play();
            }
            else if (_value == 5)
            {
                jumpSounds[Random.Range(0,jumpSounds.Length)].Play();
            }
        }
    }
}