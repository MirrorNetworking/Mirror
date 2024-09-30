using System.Collections;
using UnityEngine;

namespace Mirror.Examples.PickupsDropsChilds
{
    [RequireComponent(typeof(Rigidbody))]
    public class SceneObject : NetworkBehaviour
    {
        public GameObject ballPrefab;
        public GameObject batPrefab;
        public GameObject boxPrefab;

        [SyncVar(hook = nameof(OnChangeEquipment))]
        public EquippedItem equippedItem;

        void OnChangeEquipment(EquippedItem oldEquippedItem, EquippedItem newEquippedItem)
        {
            StartCoroutine(ChangeEquipment(newEquippedItem));
        }

        // Since Destroy is delayed to the end of the current frame, we use a coroutine
        // to clear out any child objects before instantiating the new one
        IEnumerator ChangeEquipment(EquippedItem newEquippedItem)
        {
            while (transform.childCount > 0)
            {
                Destroy(transform.GetChild(0).gameObject);
                yield return null;
            }

            // Use the new value, not the SyncVar property value
            SetEquippedItem(newEquippedItem);
        }

        // SetEquippedItem is called on the client from OnChangeEquipment (above),
        // and on the server from CmdDropItem in the PlayerEquip script.
        public void SetEquippedItem(EquippedItem newEquippedItem)
        {
            switch (newEquippedItem)
            {
                case EquippedItem.ball:
                    Instantiate(ballPrefab, transform);
                    break;
                case EquippedItem.bat:
                    Instantiate(batPrefab, transform);
                    break;
                case EquippedItem.box:
                    Instantiate(boxPrefab, transform);
                    break;
            }
        }

        protected override void OnValidate()
        {
            if (Application.isPlaying) return;

            base.OnValidate();

            if (TryGetComponent(out Rigidbody rb))
                rb.isKinematic = true;

            if (TryGetComponent(out NetworkTransformBase nt))
                nt.syncDirection = SyncDirection.ServerToClient;
        }

        public override void OnStartServer()
        {
            if (TryGetComponent(out Rigidbody rb))
            {
                rb.isKinematic = false;
                rb.AddForce(Vector3.forward, ForceMode.Impulse);
            }
        }

        void OnMouseDown()
        {
            NetworkClient.localPlayer.GetComponent<PickupsDropsChilds>().CmdPickupItem(gameObject);
        }
    }
}
