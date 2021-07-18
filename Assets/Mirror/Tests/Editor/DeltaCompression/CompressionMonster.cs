// prepare a test entity with some interesting values
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
    }
}
