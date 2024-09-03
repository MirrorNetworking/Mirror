// can be added to weapons to define more details like muzzle location, etc.
using UnityEngine;

namespace Mirror.Examples.Shooter
{
    public class WeaponDetails : MonoBehaviour
    {
        [Header("Muzzle")]
        public MuzzleFlash muzzleFlash;

        [Header("Physics")]
        public float impactForce = 2000;
    }
}
