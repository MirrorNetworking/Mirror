using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Examples.PhysicsPickupParty
{
    public class Zones : MonoBehaviour
    {
        private PickupObject pickupObject;
        public TextMesh textMesh;
        public byte zonesID = 0;
        public ZonesManager gameManager;

        void OnTriggerEnter(Collider other)
        {
            //print("OnTriggerEnter: " + other.gameObject.name);

            // should be a tag, but we're not using tags in examples incase they do not copy across during import
            pickupObject = other.GetComponent<PickupObject>();
            if (pickupObject != null)
            {
                gameManager.UpdateScores(zonesID, 1);
            }
        }

        void OnTriggerExit(Collider other)
        {
            //print("OnTriggerExit: " + other.gameObject.name);

            // should be a tag, but we're not using tags in examples incase they do not copy across during import
            pickupObject = other.GetComponent<PickupObject>();
            if (pickupObject != null)
            {
                gameManager.UpdateScores(zonesID, -1);
            }
        }
    }
}