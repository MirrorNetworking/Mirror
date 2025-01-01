﻿using System.Collections;
using UnityEngine;

namespace Mirror.Examples.PickupsDropsChilds
{
    public enum EquippedItem : byte
    {
        nothing,
        ball,
        bat,
        box
    }

    public class PickupsDropsChilds : NetworkBehaviour
    {
        [Header("Player Components")]
        public GameObject rightHand;

        [Header("Prefabs")]
        public GameObject ballPrefab;
        public GameObject batPrefab;
        public GameObject boxPrefab;
        public GameObject sceneObjectPrefab;

        [Header("Diagnostics")]
        [ReadOnly, SyncVar(hook = nameof(OnChangeEquipment))]
        public EquippedItem equippedItem;

        void Update()
        {
            if (!isLocalPlayer) return;

            if (Input.GetKeyDown(KeyCode.Alpha0) && equippedItem != EquippedItem.nothing)
                CmdChangeEquippedItem(EquippedItem.nothing);
            if (Input.GetKeyDown(KeyCode.Alpha1) && equippedItem != EquippedItem.ball)
                CmdChangeEquippedItem(EquippedItem.ball);
            if (Input.GetKeyDown(KeyCode.Alpha2) && equippedItem != EquippedItem.bat)
                CmdChangeEquippedItem(EquippedItem.bat);
            if (Input.GetKeyDown(KeyCode.Alpha3) && equippedItem != EquippedItem.box)
                CmdChangeEquippedItem(EquippedItem.box);

            if (Input.GetKeyDown(KeyCode.X) && equippedItem != EquippedItem.nothing)
                CmdDropItem();
        }

        void OnChangeEquipment(EquippedItem _, EquippedItem newEquippedItem)
        {
            StartCoroutine(ChangeEquipment());
        }

        // Since Destroy is delayed to the end of the current frame, we use a coroutine
        // to clear out any child objects before instantiating the new one
        IEnumerator ChangeEquipment()
        {
            while (rightHand.transform.childCount > 0)
            {
                Destroy(rightHand.transform.GetChild(0).gameObject);
                yield return null;
            }

            switch (equippedItem)
            {
                case EquippedItem.ball:
                    Instantiate(ballPrefab, rightHand.transform);
                    break;
                case EquippedItem.bat:
                    Instantiate(batPrefab, rightHand.transform);
                    break;
                case EquippedItem.box:
                    Instantiate(boxPrefab, rightHand.transform);
                    break;
            }
        }

        [Command]
        void CmdChangeEquippedItem(EquippedItem selectedItem)
        {
            equippedItem = selectedItem;
        }

        [Command]
        void CmdDropItem()
        {
            // Instantiate the scene object on the server
            Vector3 pos = rightHand.transform.position;
            Quaternion rot = rightHand.transform.rotation;
            GameObject newSceneObject = Instantiate(sceneObjectPrefab, pos, rot);

            // set the RigidBody as non-kinematic on the server only (isKinematic = true in prefab)
            newSceneObject.GetComponent<Rigidbody>().isKinematic = false;

            SceneObject sceneObject = newSceneObject.GetComponent<SceneObject>();

            // set the SyncVar on the scene object for clients to instantiate
            sceneObject.equippedItem = equippedItem;

            // set the direction to launch the scene object
            sceneObject.direction = rightHand.transform.forward;

            // set the player's SyncVar to nothing so clients will destroy the equipped child item
            equippedItem = EquippedItem.nothing;

            // set the child object on the server
            sceneObject.SetEquippedItem();

            // Spawn the scene object on the network for all to see
            NetworkServer.Spawn(newSceneObject);
        }

        // public because it's called from a script on the SceneObject
        [Command]
        public void CmdPickupItem(GameObject sceneObject)
        {
            // set the player's SyncVar so clients can show the equipped item
            equippedItem = sceneObject.GetComponent<SceneObject>().equippedItem;

            // Destroy the scene object
            NetworkServer.Destroy(sceneObject);
        }
    }
}
