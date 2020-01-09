using UnityEngine;
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

    }

    // https://github.com/vis2k/Mirror/issues/1151
    class MockPlayerForIssue1151 : NetworkBehaviour
    {
        [SyncVar(hook = nameof(Hook))]
        public int x = 5;

        public override void OnStartAuthority() {
            Debug.LogWarning("1 Authority started");
            CmdSet();
        }

        [Command]
        void CmdSet()
        {
            Debug.LogWarning("2 CmdSet: " + x);
            // set x to 10. setting it will call the Hook immediately.
            // -> the hook will call CmdCheck
            // -> CmdCheck returns
            // -> Hook() returns
            // then we are back in CmdSet
            x = 10;
        }

        void Hook(int newX)
        {
            Debug.LogWarning("3 Hook:" + newX);
            CmdCheck(newX);
        }

        public int result;
        [Command]
        void CmdCheck(int newX)
        {
            // x should be 10, new X should be 10
            Debug.LogWarning("4 CmdCheck x=" + x + " newX=" + newX);
            this.result = newX;
        }
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

        [Test]
        public void TestSyncIntervalAndClearDirtyComponents()
        {

            GameObject gameObject = new GameObject();

            MockPlayer player = gameObject.AddComponent<MockPlayer>();
            player.lastSyncTime = Time.time;
            // synchronize immediately
            player.syncInterval = 1f;

            player.guild = new MockPlayer.Guild
            {
                name = "Back street boys"
            };

            Assert.That(player.IsDirty(), Is.False, "Sync interval not met, so not dirty yet");

            // ClearDirtyComponents should do nothing since syncInterval is not
            // elapsed yet
            player.netIdentity.ClearDirtyComponentsDirtyBits();

            // set lastSyncTime far enough back to be ready for syncing
            player.lastSyncTime = Time.time - player.syncInterval;

            // should be dirty now
            Assert.That(player.IsDirty(), Is.True, "Sync interval met, should be dirty");
        }

        [Test]
        public void TestSyncIntervalAndClearAllComponents()
        {

            GameObject gameObject = new GameObject();

            MockPlayer player = gameObject.AddComponent<MockPlayer>();
            player.lastSyncTime = Time.time;
            // synchronize immediately
            player.syncInterval = 1f;

            player.guild = new MockPlayer.Guild
            {
                name = "Back street boys"
            };

            Assert.That(player.IsDirty(), Is.False, "Sync interval not met, so not dirty yet");

            // ClearAllComponents should clear dirty even if syncInterval not
            // elapsed yet
            player.netIdentity.ClearAllComponentsDirtyBits();

            // set lastSyncTime far enough back to be ready for syncing
            player.lastSyncTime = Time.time - player.syncInterval;

            // should be dirty now
            Assert.That(player.IsDirty(), Is.False, "Sync interval met, should still not be dirty");
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
            NetworkWriter ownerWriter = new NetworkWriter();
            NetworkWriter observersWriter = new NetworkWriter(); // not really used in this Test
            identity1.OnSerializeAllSafely(true, ownerWriter, out int ownerWritten, observersWriter, out int observersWritten);

            // set up a "client" object
            GameObject gameObject2 = new GameObject();
            NetworkIdentity identity2 = gameObject2.AddComponent<NetworkIdentity>();
            MockPlayer player2 = gameObject2.AddComponent<MockPlayer>();

            // apply all the data from the server object
            NetworkReader reader = new NetworkReader(ownerWriter.ToArray());
            identity2.OnDeserializeAllSafely(reader, true);

            // check that the syncvars got updated
            Assert.That(player2.guild.name, Is.EqualTo("Back street boys"), "Data should be synchronized");
        }

        [Test]
        public void TestSyncModeObserversMask()
        {
            GameObject gameObject1 = new GameObject();
            NetworkIdentity identity = gameObject1.AddComponent<NetworkIdentity>();
            MockPlayer player1 = gameObject1.AddComponent<MockPlayer>();
            player1.syncInterval = 0;
            MockPlayer player2 = gameObject1.AddComponent<MockPlayer>();
            player2.syncInterval = 0;
            MockPlayer player3 = gameObject1.AddComponent<MockPlayer>();
            player3.syncInterval = 0;

            // sync mode
            player1.syncMode = SyncMode.Observers;
            player2.syncMode = SyncMode.Owner;
            player3.syncMode = SyncMode.Observers;

            Assert.That(identity.GetSyncModeObserversMask(), Is.EqualTo(0b101));
        }

        [Test]
        public void TestHostModeValueIsSetBeforeHook()
        {
            // test to prevent issue https://github.com/vis2k/Mirror/issues/1151

            GameObject gameObject = new GameObject();
            MockPlayerForIssue1151 player = gameObject.AddComponent<MockPlayerForIssue1151>();
            Assert.That(player.x, Is.EqualTo(10));
            Assert.That(player.result, Is.EqualTo(10));
        }
    }
}
