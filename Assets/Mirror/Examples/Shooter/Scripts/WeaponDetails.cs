// can be added to weapons to define more details like muzzle location, etc.
using UnityEngine;

namespace Mirror.Examples.Shooter
{
    public class WeaponDetails : MonoBehaviour
    {
        [Header("Muzzle")]
        public MuzzleFlash muzzleFlash;

        [Header("Physics")]
        public float impactForce = 500;

        [Header("Stats")]
        public int weaponDamagePerShot = 1;
        public int weaponAmmoMax = 20;
        public int weaponRange = 99;
        public float weaponShotCooldown = 0.5f;
        public float weaponRecoil = 0.2f;
    }
}
