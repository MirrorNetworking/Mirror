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
        public void TestSettingGuild()
        {

            GameObject gameObject = new GameObject();

            MockPlayer player = gameObject.AddComponent<MockPlayer>();

            Assert.That(!player.IsDirty());

            MockPlayer.Guild myGuild = new MockPlayer.Guild
            {
                name = "Back street boys"
            };

            player.guild = myGuild;

            Assert.That(player.IsDirty());
        }

    }
}
