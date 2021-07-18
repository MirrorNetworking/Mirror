using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public struct InventorySlot
    {
        public int itemId;
        public int amount;
    }

    // test monster for compression.
    // can't be in separate file because Unity complains about it being an Editor
    // script because it's in the Editor folder.
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

        public void Initialize(
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

    public class DeltaCompressionTests
    {
        // two snapshots
        protected CompressionMonster A;
        protected CompressionMonster B;

        [SetUp]
        public void SetUp()
        {
            // create the monster with unique values
            A = new GameObject().AddComponent<CompressionMonster>();
            A.Initialize(
                // name, health, mana, level
                "Skeleton",
                100,
                200,
                60,
                // position, rotation
                new Vector3(10, 20, 30),
                Quaternion.identity,
                // inventory
                new List<InventorySlot>{
                    new InventorySlot{amount=0, itemId=0},
                    new InventorySlot{amount=1, itemId=42},
                    new InventorySlot{amount=50, itemId=43},
                    new InventorySlot{amount=0, itemId=0}
                },
                // strength, intelligence, damage, defense
                10,
                11,
                1000,
                500
            );

            // change it a little for second snapshot
            B = new GameObject().AddComponent<CompressionMonster>();
            B.Initialize(
                // name, health, mana, level
                "Skeleton (Dead)",
                0,
                99,
                61,
                // position, rotation
                new Vector3(11, 22, 30),
                Quaternion.identity,
                // inventory
                new List<InventorySlot>{
                    new InventorySlot{amount=5, itemId=42},
                    new InventorySlot{amount=6, itemId=43},
                },
                // strength, intelligence, damage, defense
                12,
                13,
                5000,
                2000
            );
        }

        // quick test to write the uncompressed component.
        // to make sure mirror generates serialization etc.
        [Test]
        public void Uncompressed()
        {
            NetworkWriter writerA = new NetworkWriter();
            A.OnSerialize(writerA, true);
            Debug.Log($"A uncompressed size: {writerA.Position} bytes");

            NetworkWriter writerB = new NetworkWriter();
            B.OnSerialize(writerB, true);
            Debug.Log($"B uncompressed size: {writerB.Position} bytes");
        }

        // TODO several compressions
        // TODO with benchmark each
    }
}
