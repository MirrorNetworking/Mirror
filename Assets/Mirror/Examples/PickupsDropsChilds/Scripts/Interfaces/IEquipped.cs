namespace Mirror.Examples.PickupsDropsChilds
{
    interface IEquipped
    {
        EquippedItemConfig equippedItemConfig { get; set; }

        void Use();
        void AddUsages(byte usages);
        void ResetUsages();
        void ResetUsages(byte usages);
    }

    [System.Serializable]
    public struct EquippedItemConfig : System.IEquatable<EquippedItemConfig>
    {
        // Usages remaining...this could be ammo, potion doses, magic item charges, etc.
        public byte usages;

        // Maximum usages...set to 0 for effectively unlimited uses
        public byte maxUsages;

        public EquippedItemConfig(byte maxUsages)
        {
            usages = maxUsages;
            this.maxUsages = maxUsages;
        }

        public EquippedItemConfig(byte usages, byte maxUsages)
        {
            this.usages = usages;
            this.maxUsages = maxUsages;
        }

        public void Use()
        {
            // Reset usages to within allowed range in case higher than maxUsages
            ResetUsages(usages);

            // if we have usages left, decrement
            if (usages > 0)
                usages--;
        }

        // Add a specific number of usages
        public void AddUsages(byte usages)
        {
            // Limit usages to maxUsages
            this.usages = (byte)Mathd.Clamp(this.usages + usages, 0, maxUsages);
        }

        // Fully reload to max usages
        public void ResetUsages()
        {
            this.usages = maxUsages;
        }

        // Reload to a specific number of usages
        public void ResetUsages(byte usages)
        {
            // Limit usages to maxUsages
            this.usages = (byte)Mathd.Clamp(usages, 0, maxUsages);
        }

        public bool Equals(EquippedItemConfig other)
        {
            return usages == other.usages && maxUsages == other.maxUsages;
        }

        public override string ToString()
        {
            return $"EquippedItemConfig[{usages}/{maxUsages}]";
        }
    }
}