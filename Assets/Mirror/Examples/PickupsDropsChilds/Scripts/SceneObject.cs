using System.Collections;
using UnityEngine;

namespace Mirror.Examples.PickupsDropsChilds
{
    [RequireComponent(typeof(Rigidbody))]
    public class SceneObject : NetworkBehaviour
    {
        [Header("Prefabs")]
        public GameObject ballPrefab;
        public GameObject batPrefab;
        public GameObject boxPrefab;

        [Header("Settings")]
        [Range(0, 5)] public float force = 1;

        [Header("Diagnostics")]
        [ReadOnly] public Vector3 direction;

        [ReadOnly, SyncVar(hook = nameof(OnChangeEquipment))]
        public EquippedItem equippedItem;

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
                rb.AddForce(direction * force, ForceMode.Impulse);
            }
        }

        void OnMouseDown()
        {
            NetworkClient.localPlayer.GetComponent<PickupsDropsChilds>().CmdPickupItem(gameObject);
        }

        void OnChangeEquipment(EquippedItem _, EquippedItem newEquippedItem)
        {
            StartCoroutine(ChangeEquipment());
        }

        // Since Destroy is delayed to the end of the current frame, we use a coroutine
        // to clear out any child objects before instantiating the new one
        IEnumerator ChangeEquipment()
        {
            while (transform.childCount > 0)
            {
                Destroy(transform.GetChild(0).gameObject);
                yield return null;
            }

            SetEquippedItem();
        }

        // SetEquippedItem is called on the client from OnChangeEquipment (above),
        // and on the server from CmdDropItem in the PlayerEquip script.
        public void SetEquippedItem()
        {
            switch (equippedItem)
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
    }
}
