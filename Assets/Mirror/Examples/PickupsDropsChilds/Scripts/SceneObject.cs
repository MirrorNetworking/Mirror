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
        [ReadOnly] public Vector3 direction;

        // Cached reference to IEquipped component on the child object
        [ReadOnly, SerializeField] IEquipped iEquipped;

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

        void OnChangeEquippedItemConfig(EquippedItemConfig _, EquippedItemConfig newEquippedItemConfig)
        {
            // equippedItem may be EquippedItem.nothing so check for not null
            // before getting reference to the IEquipped interface component
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
            while (transform.childCount > 0)
            {
                Destroy(transform.GetChild(0).gameObject);
                yield return null;
            }

            equippedObject = null;

            switch (equippedItem)
            {
                case EquippedItem.ball:
                    equippedObject = Instantiate(ballPrefab, transform);
                    break;
                case EquippedItem.bat:
                    equippedObject = Instantiate(batPrefab, transform);
                    break;
                case EquippedItem.box:
                    equippedObject = Instantiate(boxPrefab, transform);
                    break;
            }

            // equippedItem may be EquippedItem.nothing so check for not null
            // before getting reference to the IEquipped interface component
            // and only set the equippedItemConfig if it's different.
            if (equippedObject != null && equippedObject.TryGetComponent(out iEquipped))
                if (!iEquipped.equippedItemConfig.Equals(equippedItemConfig))
                    iEquipped.equippedItemConfig = equippedItemConfig;
        }
    }
}
