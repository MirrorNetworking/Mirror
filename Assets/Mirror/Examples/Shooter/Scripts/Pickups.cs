using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace Mirror.Examples.Shooter
{
    public class Pickups : MonoBehaviour
    {
        public int increaseAmount = 10;
        public PickupType pickupType;
        public PickupManager pickupManager;
        private Player player;
        public int weaponNumber; // see ShooterCharacterData for array id's //pistol 1, uzi 2, shotgun 3, mg4 , sniper 5

        [ServerCallback]
        void OnTriggerEnter(Collider collider)
        {
            if (collider.CompareTag("Player"))
            {
                player = collider.GetComponent<Player>();
                if (player)
                {
                    if (pickupType == PickupType.Ammo)
                    {
                        player.playerAmmo += increaseAmount;
                    }
                    else if (pickupType == PickupType.Health)
                    {
                        player.playerHealth += increaseAmount;
                    }
                    else if (pickupType == PickupType.Weapon)
                    {
                        player.playerHealth += increaseAmount;
                        player.playerWeapon.currentWeapon = weaponNumber;
                        if (pickupManager.isServerOnly) player.playerWeapon.SetupNewWeapon();
                    }

                    pickupManager.DisablePickup(transform.parent.gameObject);
                    //transform.parent.gameObject.SetActive(false);
                }
            }
        }
    }

    public enum PickupType
    {
        Ammo,
        Health,
        Weapon
    }
}