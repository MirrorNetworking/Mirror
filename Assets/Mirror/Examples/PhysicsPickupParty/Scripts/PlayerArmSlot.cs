using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace Mirror.Examples.PhysicsPickupParty
{
    public class PlayerArmSlot : NetworkBehaviour
    {
        public PlayerPickupParty playerPickupParty;
        public Collider armTrigger;
        //public Rigidbody pickedUpRigidbody;
        public PickupObject pickupObject;
        //public Transform collidedGameObject;

        public override void OnStartLocalPlayer()
        {
            // disable trigger by default, and enable if client
            // this is to lighten collision and calculations on dedicated server and non owners
            armTrigger.enabled = true;
        }

        void OnTriggerEnter(Collider other)
        {
            //print("OnTriggerEnter: " + other.gameObject.name);
            if (pickedUpNetworkObject != null) return;



            // should be a tag, but we're not using tags in examples incase they do not copy across during import
            pickupObject = other.GetComponent<PickupObject>();
            if (pickupObject != null)
            //if (  other.gameObject.name.Contains("PickupObject"))
            {
                playerPickupParty.canPickup = true;
                //collidedGameObject = other.transform;
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (pickupObject == null) return;
            //print("OnTriggerExit: " + other.gameObject.name);

            // should be a tag, but we're not using tags in examples incase they do not copy across during import
            pickupObject = other.GetComponent<PickupObject>();
            if (pickupObject != null)
            // if (other.gameObject.name.Contains("PickupObject"))
            {
                playerPickupParty.canPickup = false;
                pickupObject = null;
            }
        }

        [SyncVar(hook = nameof(OnPickupChangedHook))]
        public NetworkIdentity pickedUpNetworkObject;

        void OnPickupChangedHook(NetworkIdentity _old, NetworkIdentity _new)
        {
            if (pickedUpNetworkObject)
            {
                //Debug.Log("OnPickupChangedHook: " + pickedUpNetworkObject);
                PickupResult();
            }
            else
            {
                DropResult();
            }


        }

        private void Update()
        {
            if (pickedUpNetworkObject)
            {
                pickedUpNetworkObject.transform.position = this.transform.position;
            }
        }

        public void PickupResult()
        {
            // we cache rigidbody on pickup, not on trigger detection, so GetComponent will be called less frequently
            pickupObject = pickedUpNetworkObject.GetComponent<PickupObject>();
            if (pickupObject)
            {
                pickupObject.playerHolder = this.transform.root.gameObject;
                pickupObject.GetComponent<Collider>().enabled = false;
                if (pickupObject.pickupRigidbody)
                {
                    pickupObject.pickupRigidbody.useGravity = false;
                    pickupObject.pickupRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
                }
                if (pickupObject.networkTransform) { pickupObject.networkTransform.enabled = false; }
                armTrigger.enabled = false;
            }
        }

        public void DropResult()
        {
            if (pickupObject)
            {
                if (pickupObject.pickupRigidbody)
                {
                    pickupObject.pickupRigidbody.useGravity = true;
                    pickupObject.pickupRigidbody.constraints = RigidbodyConstraints.None;
                }
                pickupObject.GetComponent<Collider>().enabled = true;
                armTrigger.enabled = true;
                pickedUpNetworkObject = null;
                if (pickupObject.networkTransform) { pickupObject.networkTransform.enabled = true; }
                pickupObject.playerHolder = null;
                pickupObject = null;
            }
        }

        [Command]
        public void CmdPickup(NetworkIdentity networkIdentity)
        {
            PickupObject pickupObject = networkIdentity.GetComponent<PickupObject>();
            if (pickupObject.playerHolder == null)
            {
                pickedUpNetworkObject = networkIdentity;
                if (isServerOnly)
                {
                    PickupResult();
                }
            }
        }

        [Command]
        public void CmdDrop()
        {
            pickedUpNetworkObject = null;
            if (isServerOnly)
            {
                DropResult();
            }
        }
    }
}