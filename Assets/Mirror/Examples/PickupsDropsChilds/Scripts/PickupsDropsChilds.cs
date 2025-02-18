using System.Collections;
using UnityEngine;

namespace Mirror.Examples.PickupsDropsChilds
{
    public class PickupsDropsChilds : NetworkBehaviour
    {
        [Header("Player Components")]
        public GameObject rightHand;

        [Header("Prefabs")]
        public GameObject ballPrefab;
        public GameObject batPrefab;
        public GameObject boxPrefab;
        public GameObject sceneObjectPrefab;

        // IMPORTANT: Order of SyncVar declarations is intentional!
        // ChangeEquipment coroutine depends on equippedItemConfig being set first
        // but equippedItemConfig can also be changed independent of equippedItem
        // changing, e.g. reloading usages.
        [Header("SyncVars in Specific Order")]
        [SyncVar(hook = nameof(OnChangeEquippedItemConfig))]
        public EquippedItemConfig equippedItemConfig = default;
        [SyncVar(hook = nameof(OnChangeEquipment))]
        public EquippedItem equippedItem;

        [Header("Diagnostics")]
        [ReadOnly] public GameObject equippedObject;

        // Cached reference to IEquipped component on the child object
        IEquipped iEquipped;

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

            if (Input.GetKeyDown(KeyCode.U) && iEquipped != null)
                CmdUseItem();

            if (Input.GetKeyDown(KeyCode.I) && iEquipped != null)
                CmdAddUsages(1);

            if (Input.GetKeyDown(KeyCode.O) && iEquipped != null)
                CmdResetUsages();

            if (Input.GetKeyDown(KeyCode.P) && iEquipped != null)
                CmdResetUsages(3);

            if (Input.GetKeyDown(KeyCode.X) && equippedItem != EquippedItem.nothing)
                CmdDropItem();
        }

        void OnChangeEquippedItemConfig(EquippedItemConfig _, EquippedItemConfig newEquippedItemConfig)
        {
            // equippedItem may be EquippedItem.nothing so check for not null
            // before getting reference to the IEquipped interface component
            // and only set the equippedItemConfig if it's different.
            if (equippedObject != null && equippedObject.TryGetComponent(out iEquipped))
                if (!iEquipped.equippedItemConfig.Equals(equippedItemConfig))
                    iEquipped.equippedItemConfig = equippedItemConfig;
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

            equippedObject = null;

            switch (equippedItem)
            {
                case EquippedItem.ball:
                    equippedObject = Instantiate(ballPrefab, rightHand.transform);
                    break;
                case EquippedItem.bat:
                    equippedObject = Instantiate(batPrefab, rightHand.transform);
                    break;
                case EquippedItem.box:
                    equippedObject = Instantiate(boxPrefab, rightHand.transform);
                    break;
            }

            // equippedItem may be EquippedItem.nothing so check for not null
            // before getting reference to the IEquipped interface component
            // and only set the equippedItemConfig if it's different.
            if (equippedObject != null && equippedObject.TryGetComponent(out iEquipped))
                if (!iEquipped.equippedItemConfig.Equals(equippedItemConfig))
                    iEquipped.equippedItemConfig = equippedItemConfig;
        }

        [Command]
        void CmdChangeEquippedItem(EquippedItem selectedItem)
        {
            switch (selectedItem)
            {
                case EquippedItem.ball:
                    if (ballPrefab.TryGetComponent(out IEquipped ball))
                        equippedItemConfig = ball.equippedItemConfig;
                    break;
                case EquippedItem.bat:
                    if (batPrefab.TryGetComponent(out IEquipped bat))
                        equippedItemConfig = bat.equippedItemConfig;
                    break;
                case EquippedItem.box:
                    if (boxPrefab.TryGetComponent(out IEquipped box))
                        equippedItemConfig = box.equippedItemConfig;
                    break;
                case EquippedItem.nothing:
                    equippedItemConfig = default;
                    break;
            }

            equippedItem = selectedItem;
        }

        [Command]
        public void CmdUseItem()
        {
            // equippedItemConfig is a struct SyncVar so this
            // is how to update it correctly on the server.
            EquippedItemConfig config = equippedItemConfig;
            config.Use();
            equippedItemConfig = config;

            // tell clients to invoke the Use method on the IEquipped object
            RpcUseItem();
        }

        [Command]
        public void CmdAddUsages(byte usages)
        {
            // equippedItemConfig is a struct SyncVar so this
            // is how to update it correctly on the server.
            EquippedItemConfig config = equippedItemConfig;
            config.AddUsages(usages);
            equippedItemConfig = config;
            // tell clients to invoke the AddUsages method on the IEquipped object
            RpcAddUsages(usages);
        }

        [Command]
        public void CmdResetUsages()
        {
            // equippedItemConfig is a struct SyncVar so this
            // is how to update it correctly on the server.
            EquippedItemConfig config = equippedItemConfig;
            config.ResetUsages();
            equippedItemConfig = config;
            // tell clients to invoke the ResetUsages method on the IEquipped object
            RpcResetUsages();
        }

        [Command]
        public void CmdResetUsages(byte usages)
        {
            // equippedItemConfig is a struct SyncVar so this
            // is how to update it correctly on the server.
            EquippedItemConfig config = equippedItemConfig;
            config.ResetUsages(usages);
            equippedItemConfig = config;
            // tell clients to invoke the ResetUsages method on the IEquipped object
            RpcResetUsages(usages);
        }

        [Command]
        void CmdDropItem()
        {
            // Instantiate the scene object on the server
            Vector3 pos = rightHand.transform.position;
            Quaternion rot = rightHand.transform.rotation;
            equippedObject = Instantiate(sceneObjectPrefab, pos, rot);

            // set the RigidBody as non-kinematic on the server only (isKinematic = true in prefab)
            equippedObject.GetComponent<Rigidbody>().isKinematic = false;

            SceneObject sceneObject = equippedObject.GetComponent<SceneObject>();

            // set the SyncVar on the scene object for clients to instantiate
            sceneObject.equippedItem = equippedItem;

            // set the equippedItemConfig for the iEquipped interface
            sceneObject.equippedItemConfig = equippedItemConfig;

            // set the direction to launch the scene object
            sceneObject.direction = rightHand.transform.forward;

            // Spawn the scene object on the network for all to see
            NetworkServer.Spawn(equippedObject);

            // set the player's SyncVar to nothing so clients will destroy the iEquipped child item
            equippedItem = EquippedItem.nothing;
            equippedItemConfig = default;
        }

        // public because it's called from a script on the SceneObject
        [Command]
        public void CmdPickupItem(GameObject obj)
        {
            if (obj.TryGetComponent(out SceneObject sceneObject))
            {
                // set the player's SyncVar so clients can show the iEquipped item
                equippedItem = sceneObject.equippedItem;

                // set the equippedItemConfig on the iEquipped object
                equippedItemConfig = sceneObject.equippedItemConfig;
            }

            // Destroy the scene object
            NetworkServer.Destroy(obj);
        }

        [ClientRpc]
        public void RpcUseItem()
        {
            // iEquipped could be null so use the null conditional operator
            iEquipped?.Use();
        }

        [ClientRpc]
        public void RpcAddUsages(byte usages)
        {
            // iEquipped could be null so use the null conditional operator
            iEquipped?.AddUsages(usages);
        }

        [ClientRpc]
        public void RpcResetUsages()
        {
            // iEquipped could be null so use the null conditional operator
            iEquipped?.ResetUsages();
        }

        [ClientRpc]
        public void RpcResetUsages(byte usages)
        {
            // iEquipped could be null so use the null conditional operator
            iEquipped?.ResetUsages(usages);
        }
    }
}
