using UnityEngine;
using System.Collections;

namespace Mirror.Examples.Shooter
{
    public class PlayerWeapon : NetworkBehaviour
    {
        public Transform weaponMount;
        public Transform handTransform;
        public Transform firstPersonTransform;
        public Player player; // use player as main reference of other player scripts
        public PlayerLook playerLook;
        public WeaponDetails currentWeaponDetails;
        private Coroutine animationCoroutine;
        private float animationSmoothTime = 0.15f;
        private float animationHolsterTime = 2.0f;
        public float weaponRayMaxDistance = 10.0f;
        private bool weaponOnCooldown = false;

        [Header("Decal")]
        public GameObject decalPrefab;
        public float decalOffset = 0.01f;

        void RotateWeaponToLookDirection()
        {
            weaponMount.LookAt(player.playerLook.lookPositionFar);
        }

        void ApplyBulletForce(Rigidbody rigid, Vector3 hitNormal, float impactForce)
        {
            rigid.AddForce(-hitNormal * impactForce, ForceMode.Impulse);
        }

        // TODO sync LookPosition/DirectionRaycasted instead of passing it here
        // TODO NOT CHEAT SAFE DONT TRUST CLIENT WITH impactForce etc.
        [Command]
        void CmdApplyBulletForce(PredictedRigidbody hitObject, Vector3 hitNormal, float impactForce)
        {
            // on server, the rigidbody is attached to the PredictedRigidbody (not separated)
            Rigidbody rigid = hitObject.GetComponent<Rigidbody>();
            ApplyBulletForce(rigid, hitNormal, impactForce);
        }

        void FireWeapon()
        {
            // raycast
            if (player.playerLook.LookPositionRaycasted(out RaycastHit hit))
            {
                Debug.Log($"fired at: {hit.collider.name}");

                if (currentWeaponDetails != null)
                {
                    // muzzle flash
                    if (currentWeaponDetails.muzzleFlash != null) currentWeaponDetails.muzzleFlash.Fire();

                    // instantiate decal when shooting static objects.
                    // if it has a rigidbody (like a ball), don't add a decal.
                    Rigidbody rigid = hit.collider.GetComponent<Rigidbody>();
                    if (rigid == null)
                    {
                        // parent to hit collider so that decals don't hang in air if we
                        // hit a moving object like a door.
                        // (.collider.transform instead of
                        // -> parent to .collider.transform instead of .transform because
                        //    for our doors, .transform would be the door parent, while
                        //    .collider is the part that actually moves. so this is safer.
                        GameObject go = Instantiate(decalPrefab, hit.point + hit.normal * decalOffset, Quaternion.LookRotation(-hit.normal));
                        go.transform.parent = hit.collider.transform;

                        // setting decal rotation can show wrong results depending on angle of hit object, setting child instead solves it
                        Transform childTransform = go.transform.GetChild(0);
                        childTransform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                        // Would prefer to call raycast on server, but current setup uses players local camera
                        CmdFiredAtWorld(hit.point + hit.normal * decalOffset, Quaternion.LookRotation(-hit.normal), childTransform.localRotation);
                        SetAnimationShoot();
                    }
                    // it has a rigidbody
                    else
                    {
                        // apply impact force if we hit a rigidbody that is networked
                        if (PredictedRigidbody.IsPredicted(rigid, out PredictedRigidbody predicted))
                        {
                            // prediction: apply bullet force locally and send command to server
                            ApplyBulletForce(rigid, hit.normal, currentWeaponDetails.impactForce);
                            if (!isServer) CmdApplyBulletForce(predicted, hit.normal, currentWeaponDetails.impactForce); // not in host mode
                        }

                        // if we hit a player, call the cmd to apply damage etc.
                        if (rigid.TryGetComponent(out Player target))
                        {
                            CmdFiredAtPlayer(target, playerLook.camera.transform.position, hit.point);
                        }
                    }
                }
            }
        }

        void LateUpdate()
        {
            if (player.playerStatus != 0) return;

            // TODO rotate weapon for other players too, but PlayerLook needs to sync lookDirection first
            if (!isLocalPlayer) return;

            if (player.playerLook.distance == 0)
            {
                RotateWeaponToLookDirection();
               // SetFirstPerson(); // ideally we dont want to keep setting this if its already set, to-do
            }
            else
            {
               // SetThirdPerson(); // ideally we dont want to keep setting this if its already set, to-do
            }

            if (currentWeaponDetails == null) return;

            if (player.playerAmmo > 0 && Input.GetMouseButton(0) && weaponOnCooldown == false)
            {
                FireWeapon();
                StartCoroutine(ApplyWeaponCooldown(currentWeaponDetails.weaponShotCooldown));
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                int weaponNumber = currentWeapon;
                //print("Change weapon: " + weaponNumber);
                if (weaponNumber < player.sceneScript.characterData.weaponPrefabs.Length-1)
                {
                    weaponNumber += 1;
                }
                else
                {
                    weaponNumber = 1;
                }
                CmdChangeCurrentWeapon(weaponNumber);
            }
        }

        IEnumerator ApplyWeaponCooldown(float _time)
        {
            //print("ApplyWeaponCooldown: " + _time + " - " + weaponOnCooldown);
            weaponOnCooldown = true;
            yield return new WaitForSeconds(_time);
            weaponOnCooldown = false;
        }

        // fired at another player
        [Command]
        void CmdFiredAtPlayer(Player target, Vector3 originPoint, Vector3 hitPoint)
        {
            //print("CmdFiredAtPlayer");
            // firing is only allowed if we have ammo
            if (player.playerAmmo <= 0) return;
            // always decrease ammo no matter what
            player.playerAmmo -= 1;
            if (isServerOnly)
            {
                if (weaponOnCooldown) return;
                //server runs cooldown too, to prevent players speed cheating shots
                // we apply a slightly less time on server to cover differences in latency and performance
                StartCoroutine(ApplyWeaponCooldown(currentWeaponDetails.weaponShotCooldown * 0.9f));
            }

            if (target != null)
            {
                // if the client claims to have hit a player, then we need to verify.
                // it's important to understand that clients always view the past,
                // since they only get the recent world state after 'latency' ms.
                //
                // so when they hit another player that's moving fast,
                // it's very likely that in reality the other player is already elsewhere.
                //
                // to compensate for this latency affect, we use lag compensation!
                // it keeps a history of player positions, and rewinds time when checking.
                //
                // if A fires at B, we call B.ToleranceCheck(A.connection).

                // Lag Compensation: Bounds Check
                // if (target.compensator.BoundsCheck(connectionToClient, hitPoint, 0.1f, out float distance, out Vector3 nearest))
                // {
                //     Debug.Log($"{name} fired at {target.name} about {(connectionToClient.rtt * 1000):F0}ms ago, when {target.name} was at {nearest}. Compensation distance: {distance:F2} / {lagCompensationTolerance:F2}");
                // }
                // else Debug.Log($"{name} fired at {target.name} about {(connectionToClient.rtt * 1000):F0}ms ago, but missed. Either because it's too far out of the history, or the distance was too large.");

                // Lag Compensation: Raycast Check
                // TODO this doesn't validate player's origin point at all yet. technically would need to lag compensate the viewer position too.
                // TODO lag compensation raycast check should ignore self, same way we call it on client.
                int layerMask = ~0; // everything
                if (target.compensator.RaycastCheck(connectionToClient, originPoint, hitPoint, 0.10f, layerMask, out RaycastHit hit, weaponRayMaxDistance))
                {
                    Debug.LogWarning($"{name} fired at {target.name} about {(connectionToClient.rtt * 1000):F0}ms ago, and hit according to raycast: {hit}.");

                    if (player.bot)
                    {
                        // if player has bot script set, call aggro
                        // aggro forces movement, prevents ai from looking dumb, standing still whilst getting shot
                        player.bot.ActivateAggro();
                    }
                    
                    if (target.playerHealth > 0)
                    {
                        target.playerHealth -= 1;
                        // only minus health if there is health, to stop UI from going into minuses
                    }
                    if (target.playerHealth <= 0)
                    {
                        target.playerStatus = 1;

                        player.playerKills += 1;
                        player.sceneScript.playerScores.UpdateScore(player.playerName, player.playerKills, player.playerDeaths, player.netId);

                        target.playerDeaths += 1;
                        player.sceneScript.playerScores.UpdateScore(target.playerName, target.playerKills, target.playerDeaths, target.netId);
                        // set death sync var status
                    }

                }
                else Debug.Log($"{name} fired at {target.name} about {(connectionToClient.rtt * 1000):F0}ms ago, but missed according to raycast.");
            }

            // broadcast the shot to other players to play effects
            RpcFiredAtPlayer();
        }

        [ClientRpc(includeOwner = false)]
        void RpcFiredAtPlayer()
        {
            if (currentWeaponDetails != null)
            {
                // muzzle flash
                if (currentWeaponDetails.muzzleFlash != null) currentWeaponDetails.muzzleFlash.Fire();
            }
        }

        // fired somewhere into the world, not hitting any player
        [Command]
        void CmdFiredAtWorld(Vector3 _decalPos, Quaternion _decalQuat, Quaternion _childQuat)
        {
            //print("CmdFiredAtWorld");

            // firing is only allowed if we have ammo
            if (player.playerAmmo <= 0) return;
            player.playerAmmo -= 1;

            if (isServerOnly)
            {
                if (weaponOnCooldown) return;
                //server runs cooldown too, to prevent players speed cheating shots
                // we apply a slightly less time on server to cover differences in latency and performance
                StartCoroutine(ApplyWeaponCooldown(currentWeaponDetails.weaponShotCooldown * 0.9f));
            }
            
            RpcFiredAtWorld(_decalPos, _decalQuat, _childQuat);
        }

        [ClientRpc(includeOwner = false)]
        void RpcFiredAtWorld(Vector3 _decalPos, Quaternion _decalQuat, Quaternion _childQuat)
        {
            //print("RpcFireWeapon");
            if (currentWeaponDetails != null)
            {
                // muzzle flash
                if (currentWeaponDetails.muzzleFlash != null) currentWeaponDetails.muzzleFlash.Fire();

                GameObject go = Instantiate(decalPrefab, _decalPos, _decalQuat);
                Transform childTransform = go.transform.GetChild(0);
                childTransform.localRotation = _childQuat;
                SetAnimationShoot();
            }
        }

        public void SetThirdPerson()
        {
            weaponMount.SetParent(handTransform);
            weaponMount.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            player.skinnedMeshRenderer.enabled = true;
            if (player.sceneScript)
            {
                player.sceneScript.weaponCamera.SetActive(false);
            }
        }

        public void SetFirstPerson()
        {
            weaponMount.SetParent(firstPersonTransform);
            weaponMount.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            player.skinnedMeshRenderer.enabled = false;
            if (player.sceneScript)
            {
                player.sceneScript.weaponCamera.SetActive(true);
            }
        }

        public void SetAnimationShoot()
        {
            if (isOwned && player.playerLook.distance == 0)
            {
                // first person
                StartCoroutine(ApplyRecoilWithDelay());
            }
            else
            {
                // third person, or another players point of view
                player.playerMovement.animator.Play("ThirdPersonArmAnimationRecoil");

                // If the coroutine is already running, stop it before starting a new one
                if (animationCoroutine != null)
                {
                    StopCoroutine(animationCoroutine);
                }
                animationCoroutine = StartCoroutine(AnimateShootCoroutine());   
            }
        }

        IEnumerator ApplyRecoilWithDelay()
        {
            //print("ApplyRecoilWithDelay");
            Vector3 recoilOffset = -transform.forward * currentWeaponDetails.weaponRecoil;
            instantiatedWeapon.transform.position += recoilOffset;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            instantiatedWeapon.transform.position -= recoilOffset;
        }

        private IEnumerator AnimateShootCoroutine()
        {
            float elapsedTime = 0.0f;
            // Instantly set to shoot animation
            player.playerMovement.animator.SetLayerWeight(player.playerMovement.animator.GetLayerIndex("Arm Layer"), 1.0f);

            // Wait before holstering
            yield return new WaitForSeconds(animationHolsterTime);

            while (elapsedTime < animationSmoothTime)
            {
                float currentWeight = Mathf.Lerp(1.0f, 0.0f, elapsedTime / animationSmoothTime);
                player.playerMovement.animator.SetLayerWeight(player.playerMovement.animator.GetLayerIndex("Arm Layer"), currentWeight);
                elapsedTime += Time.deltaTime;
                yield return new WaitForFixedUpdate(); // better than using wait until next frame, to prevent it being different speeds?
            }

            player.playerMovement.animator.SetLayerWeight(player.playerMovement.animator.GetLayerIndex("Arm Layer"), 0.0f);
            // Reset the coroutine reference so it can be triggered again
            animationCoroutine = null;
        }


        [SyncVar(hook = nameof(OnWeaponChanged))]
        public int currentWeapon = 1;
        public GameObject instantiatedWeapon;

        void OnWeaponChanged(int _Old, int _New)
        {
            //print("OnWeaponChanged: " + currentWeapon);
            SetupNewWeapon();
        }


        [Command]
        public void CmdChangeCurrentWeapon(int _value)
        {
            //print("CmdChangeCurrentWeapon: " + _value);
            currentWeapon = _value;
            if(isServerOnly) SetupNewWeapon();
        }

        public void SetupNewWeapon()
        {
            //print("SetupNewWeapon");

            if (instantiatedWeapon != null) { Destroy(instantiatedWeapon); }
            if (player.sceneScript == null) return;

            instantiatedWeapon = Instantiate(player.sceneScript.characterData.weaponPrefabs[currentWeapon]);
            instantiatedWeapon.transform.SetParent(weaponMount);
            instantiatedWeapon.transform.localPosition = new Vector3(0, 0, 0);
            instantiatedWeapon.transform.localRotation = Quaternion.identity;

            currentWeaponDetails = instantiatedWeapon.GetComponent<WeaponDetails>();

            if (isServer)
            {
                player.playerAmmo = currentWeaponDetails.weaponAmmoMax;
            }
            
        }
    }
}
