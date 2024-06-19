using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerArmCollision : MonoBehaviour
{
    public PlayerPickupParty playerPickupParty;
    public Collider triggerCollider;

    public Transform collidedGameObject;

    void OnTriggerEnter(Collider other)
    {
        //print("OnTriggerEnter: " + other.gameObject.name);

        // should be a tag, but we're not using tags in examples incase they do not copy across during import
        if (other.gameObject.name.Contains("Pickup"))
        {
            playerPickupParty.canPickup = true;
            collidedGameObject = other.transform;
        }
    }

    void OnTriggerExit(Collider other)
    {
        //print("OnTriggerExit: " + other.gameObject.name);

        // should be a tag, but we're not using tags in examples incase they do not copy across during import
        if (other.gameObject.name.Contains("Pickup"))
        {
            playerPickupParty.canPickup = false;
            collidedGameObject = null;
        }
    }
}
