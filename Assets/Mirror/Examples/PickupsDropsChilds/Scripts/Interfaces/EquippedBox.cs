using UnityEngine;

namespace Mirror.Examples.PickupsDropsChilds
{
    public class EquippedBox : MonoBehaviour, IEquipped
    {
        // Note: This example doesn't include animations or sounds for simplicity.
        // These are just here for illustration purposes...the implementation
        // methods could do something interesting like play a sound or animation.
        [Header("Components")]
        public Animator animator;
        public AudioSource audioSource;

        [Header("Equipped Item")]
        [SerializeField]
        EquippedItemConfig _equippedItemConfig;

        public EquippedItemConfig equippedItemConfig
        {
            get => _equippedItemConfig;
            set
            {
                Debug.Log($"{transform.root.name} EquippedItemConfig set from {_equippedItemConfig} to {value}", gameObject);
                _equippedItemConfig = value;
            }
        }

        void Reset()
        {
            equippedItemConfig = new EquippedItemConfig { usages = 0, maxUsages = 0 };
        }

        // Play appropriate animation or sound
        public void Use()
        {
            // Effectively unlimited uses
            if (equippedItemConfig.maxUsages == 0)
            {
                Debug.Log("Box used");
                return;
            }

            if (equippedItemConfig.usages > 0)
                Debug.Log("Box used");
            else
                Debug.Log("Box is out of uses");
        }

        // Play appropriate animation or sound
        public void AddUsages(byte usages)
        {
            Debug.Log($"Box added {usages} usages");
        }

        // Play appropriate animation or sound
        public void ResetUsages()
        {
            Debug.Log("Box reset");
        }

        // Play appropriate animation or sound
        public void ResetUsages(byte usages)
        {
            Debug.Log($"Box reset usages to {usages}");
        }
    }
}