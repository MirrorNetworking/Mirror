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

        public bool IsDirty()
        {
            return base.syncVarDirtyBits != 0;
        }
    }


    public class SyncVarTest
    {

        [Test]
        public void TestSettingStruct()
        {

            GameObject gameObject = new GameObject();

            MockPlayer player = gameObject.AddComponent<MockPlayer>();

            Assert.That(player.IsDirty(), Is.False, "First time object should not be dirty");

            MockPlayer.Guild myGuild = new MockPlayer.Guild
            {
                name = "Back street boys"
            };

            player.guild = myGuild;

            Assert.That(player.IsDirty(), "Setting struct should mark object as dirty");
            player.ClearAllDirtyBits();
            Assert.That(player.IsDirty(), Is.False, "ClearAllDirtyBits() should clear dirty flag");

            // clearing the guild should set dirty bit too
            player.guild = default;
            Assert.That(player.IsDirty(), "Clearing struct should mark object as dirty");
        }

    }
}
