using UnityEngine;
using System.Collections;
using Mirror;
using NUnit.Framework;

namespace Mirror.Tests
{

    class MockPlayer : NetworkBehaviour
    {
        public struct Guild
        {
            public string name;
        }

        [SyncVar]
        public Guild guild;

        [SyncVar(ownerOnly = true)]
        public int health;

        [SyncVar(ownerOnly = true)]
        public int mana;

        [SyncVar]
        public string playerName;

    }


    public class SyncVarTest
    {

        [Test]
        public void TestSettingStruct()
        {

            GameObject gameObject = new GameObject();

            MockPlayer player = gameObject.AddComponent<MockPlayer>();

            // synchronize immediatelly
            player.syncInterval = 0f;

            Assert.That(player.IsDirty(false), Is.False, "First time object should not be dirty");

            MockPlayer.Guild myGuild = new MockPlayer.Guild
            {
                name = "Back street boys"
            };

            player.guild = myGuild;

            Assert.That(player.IsDirty(false), "Setting struct should mark object as dirty");
            player.ClearAllDirtyBits();
            Assert.That(player.IsDirty(false), Is.False, "ClearAllDirtyBits() should clear dirty flag");

            // clearing the guild should set dirty bit too
            player.guild = default;
            Assert.That(player.IsDirty(false), "Clearing struct should mark object as dirty");
        }

        [Test]
        public void TestSynchronizingObjects()
        {
            // set up a "server" object
            GameObject gameObject1 = new GameObject();
            NetworkIdentity identity1 = gameObject1.AddComponent<NetworkIdentity>();
            MockPlayer player1 = gameObject1.AddComponent<MockPlayer>();
            MockPlayer.Guild myGuild = new MockPlayer.Guild
            {
                name = "Back street boys"
            };
            player1.guild = myGuild;

            // serialize all the data as we would for the network
            NetworkWriter writer = new NetworkWriter();
            identity1.OnSerializeAllSafely(true, writer, true);

            // set up a "client" object
            GameObject gameObject2 = new GameObject();
            NetworkIdentity identity2 = gameObject2.AddComponent<NetworkIdentity>();
            MockPlayer player2 = gameObject2.AddComponent<MockPlayer>();

            // apply all the data from the server object
            NetworkReader reader = new NetworkReader(writer.ToArray());
            identity2.OnDeserializeAllSafely(reader, true);

            // check that the syncvars got updated
            Assert.That(player2.guild.name, Is.EqualTo("Back street boys"), "Data should be synchronized");
        }

        [Test]
        public void TestOwnerBits()
        {
            GameObject gameObject1 = new GameObject();
            NetworkIdentity _ = gameObject1.AddComponent<NetworkIdentity>();
            MockPlayer player1 = gameObject1.AddComponent<MockPlayer>();

            Assert.That(player1.getSyncVarOwnerMask(), Is.EqualTo(0b0110), "second and third variable are owner private");
        }

        [Test]
        public void TestOwnerDirtyBits()
        {
            GameObject gameObject1 = new GameObject();
            NetworkIdentity _ = gameObject1.AddComponent<NetworkIdentity>();
            MockPlayer player1 = gameObject1.AddComponent<MockPlayer>();
            player1.syncInterval = 0;

            Assert.That(player1.IsDirty(false), Is.False, "Object is not dirty from observer perspective");
            Assert.That(player1.IsDirty(true), Is.False, "Object is not dirty from owner perspective");

            player1.health = 10;

            Assert.That(player1.IsDirty(false), Is.False, "Object is not dirty from observer perspective");
            Assert.That(player1.IsDirty(true), Is.True, "Object is dirty from owner perspective");
        }

        [Test]
        public void TestDirtyComponentMask()
        {
            GameObject gameObject1 = new GameObject();
            NetworkIdentity identity = gameObject1.AddComponent<NetworkIdentity>();
            MockPlayer player1 = gameObject1.AddComponent<MockPlayer>();
            player1.syncInterval = 0;
            MockPlayer player2 = gameObject1.AddComponent<MockPlayer>();
            player2.syncInterval = 0;
            MockPlayer player3 = gameObject1.AddComponent<MockPlayer>();
            player3.syncInterval = 0;

            // owner bit
            player1.health = 10;
            player3.playerName = "Paul";

            Assert.That(identity.GetDirtyMask(true, true), Is.EqualTo(0b111), "All components are considered dirty on initialization");
            Assert.That(identity.GetDirtyMask(false, true), Is.EqualTo(0b101), "first and third component are dirty for owners");
            Assert.That(identity.GetDirtyMask(false, false), Is.EqualTo(0b100), "first component is not dirty for observers");

        }

        [Test]
        public void TestDirtySyncVarMask()
        {
            GameObject gameObject1 = new GameObject();
            NetworkIdentity identity = gameObject1.AddComponent<NetworkIdentity>();
            MockPlayer player1 = gameObject1.AddComponent<MockPlayer>();

            // owner bit
            player1.health = 10;
            player1.playerName = "Paul";

            Assert.That(player1.GetDirtySyncVarMask(true, true), Is.EqualTo(~0UL), "All components are considered dirty on initialization");
            Assert.That(player1.GetDirtySyncVarMask(true, false), Is.EqualTo(~0b0110ul), "owner only should not be considered dirty");
            Assert.That(player1.GetDirtySyncVarMask(false, true), Is.EqualTo(0b1010ul), "Onwer can see both health and player name");
            Assert.That(player1.GetDirtySyncVarMask(false, false), Is.EqualTo(0b1000ul), "Non Owner can only see player name");
        }
    }
}