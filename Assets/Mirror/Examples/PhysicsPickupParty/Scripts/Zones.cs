using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Examples.PhysicsPickupParty
{
    public class Zones : MonoBehaviour
    {
        // this script, and the collider should only run on server

        private PickupObject pickupObject;
        public TextMesh textMesh;
        public byte zonesID = 0;
        public ZonesManager gameManager;

        [ServerCallback]
        private void Start()
        {
            GetComponent<Collider>().enabled = true;
        }

        [ServerCallback]
        void OnTriggerEnter(Collider other)
        {
            //print("OnTriggerEnter: " + other.gameObject.name);

            // could be a tag, but we're not using tags in examples incase they do not copy across during import
            pickupObject = other.GetComponent<PickupObject>();
            if (pickupObject != null)
            {
                gameManager.UpdateScores(zonesID, 1);
            }
        }

        [ServerCallback]
        void OnTriggerExit(Collider other)
        {
            //print("OnTriggerExit: " + other.gameObject.name);

            // could be a tag, but we're not using tags in examples incase they do not copy across during import
            pickupObject = other.GetComponent<PickupObject>();
            if (pickupObject != null)
            {
                gameManager.UpdateScores(zonesID, -1);
            }
        }
    }
}