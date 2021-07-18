// prepare a test entity with some interesting values
using System.Collections.Generic;
using UnityEngine;

namespace Mirror.Tests
{
    public struct InventorySlot
    {
        public int itemId;
        public int amount;
    }

    public class CompressionMonster : NetworkBehaviour
    {
        // a variable length name field
        [SyncVar] public string monsterName;

        // a couple of fixed length fields
        [SyncVar] public int health;
        [SyncVar] public int mana;
        [SyncVar] public int level;
        [SyncVar] public Vector3 position;
        [SyncVar] public Quaternion rotation;

        // variable length inventory
        SyncList<InventorySlot> inventory = new SyncList<InventorySlot>();

        // a couple more fixed fields AFTER variable length inventory
        // to make sure they are still delta compressed decently.
        [SyncVar] public int strength;
        [SyncVar] public int intelligence;
        [SyncVar] public int damage;
        [SyncVar] public int defense;

        public CompressionMonster(
            string monsterName,
            int health, int mana, int level,
            Vector3 position, Quaternion rotation,
            List<InventorySlot> inventory,
            int strength, int intelligence,
            int damage, int defense)
        {
            this.monsterName = monsterName;
            this.health = health;
            this.mana = mana;
            this.level = level;
            this.position = position;
            this.rotation = rotation;

            foreach (InventorySlot slot in inventory)
                this.inventory.Add(slot);

            this.strength = strength;
            this.intelligence = intelligence;
            this.damage = damage;
            this.defense = defense;
        }
    }
}
