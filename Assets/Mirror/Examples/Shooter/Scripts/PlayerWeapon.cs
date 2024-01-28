using UnityEngine;

namespace Mirror.Examples.Shooter
{
    public class PlayerWeapon : NetworkBehaviour
    {
        public Transform weaponMount;
        public PlayerLook playerLook;

        [Header("Decal")]
        public GameObject decalPrefab;
        public float decalOffset = 0.01f;

        void RotateWeaponToLookDirection()
        {
            weaponMount.LookAt(playerLook.lookPositionFar);
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
            if (playerLook.LookPositionRaycasted(out RaycastHit hit))
            {
                Debug.Log($"fired at: {hit.collider.name}");

                // any weapon equipped?
                WeaponDetails weapon = weaponMount.GetComponentInChildren<WeaponDetails>();
                if (weapon != null)
                {
                    // muzzle flash
                    if (weapon.muzzleFlash != null) weapon.muzzleFlash.Fire();

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
                    }
                    // it has a rigidbody
                    else
                    {
                        // apply impact force if we hit a rigidbody that is networked
                        if (rigid.TryGetComponent(out PredictedRigidbodyPhysicsGhost predictedPhysics))
                        {
                            // prediction: apply bullet force locally and send command to server
                            ApplyBulletForce(rigid, hit.normal, weapon.impactForce);
                            PredictedRigidbody target = predictedPhysics.target.GetComponent<PredictedRigidbody>();
                            if (!isServer) CmdApplyBulletForce(target, hit.normal, weapon.impactForce); // not in host mode
                        }
                    }
                }
            }
        }

        void LateUpdate()
        {
            // TODO rotate weapon for other players too, but PlayerLook needs to sync lookDirection first
            if (!isLocalPlayer) return;

            RotateWeaponToLookDirection();


            if (Input.GetMouseButtonDown(0))
            {
                FireWeapon();
            }
        }
    }
}
