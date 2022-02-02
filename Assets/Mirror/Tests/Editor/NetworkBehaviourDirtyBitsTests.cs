// dirty bits are powerful magic.
// add some tests to guarantee correct behaviour.
using NUnit.Framework;

namespace Mirror.Tests
{
    class NetworkBehaviourWithSyncVarsAndCollections : NetworkBehaviour
    {
        // SyncVars
        [SyncVar] public int health;
        [SyncVar] public int mana;

        // SyncCollections
        public readonly SyncList<int> list = new SyncList<int>();
        public readonly SyncDictionary<int, string> dict = new SyncDictionary<int, string>();
    }

    public class NetworkBehaviourSyncVarDirtyBitsExposed : NetworkBehaviour
    {
        public ulong syncVarDirtyBitsExposed => syncVarDirtyBits;
    }

    public class NetworkBehaviourDirtyBitsTests : MirrorEditModeTest
    {
        NetworkConnectionToClient connectionToClient;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // SyncLists are only set dirty while owner has observers.
            // need a connection.
            NetworkServer.Listen(1);
            ConnectClientBlockingAuthenticatedAndReady(out connectionToClient);
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        [Test]
        public void SetSyncVarDirtyBit()
        {
            CreateNetworkedAndSpawn(out _, out _, out NetworkBehaviourSyncVarDirtyBitsExposed comp,
                                    out _, out _, out _);

            // set 3rd dirty bit.
            comp.SetSyncVarDirtyBit(0b_00000000_00000100);
            Assert.That(comp.syncVarDirtyBitsExposed, Is.EqualTo(0b_00000000_00000100));

            // set 5th dirty bit.
            // both 3rd and 5th should be set.
            comp.SetSyncVarDirtyBit(0b_00000000_00010000);
            Assert.That(comp.syncVarDirtyBitsExposed, Is.EqualTo(0b_00000000_00010100));
        }

        // changing a SyncObject (collection) should modify the dirty mask.
        [Test]
        public void SyncObjectsSetDirtyBits()
        {
            CreateNetworkedAndSpawn(out _, out _, out NetworkBehaviourWithSyncVarsAndCollections comp,
                                    out _, out _, out _);

            // not dirty by default
            Assert.That(comp.syncObjectDirtyBits, Is.EqualTo(0UL));

            // change the list. should be dirty now.
            comp.list.Add(42);
            Assert.That(comp.syncObjectDirtyBits, Is.EqualTo(0b01));

            // change the dict. should both be dirty.
            comp.dict[42] = null;
            Assert.That(comp.syncObjectDirtyBits, Is.EqualTo(0b11));
        }

        [Test]
        public void IsDirty()
        {
            CreateNetworkedAndSpawn(out _, out _, out NetworkBehaviourWithSyncVarsAndCollections comp,
                                    out _, out _, out _);

            // not dirty by default
            Assert.That(comp.IsDirty(), Is.False);

            // changing a [SyncVar] should set it dirty
            ++comp.health;
            Assert.That(comp.IsDirty(), Is.True);
            comp.ClearAllDirtyBits();

            // changing a SyncCollection should set it dirty
            comp.list.Add(42);
            Assert.That(comp.IsDirty(), Is.True);
            comp.ClearAllDirtyBits();

            // it should only be dirty after syncInterval elapsed
            comp.syncInterval = float.MaxValue;
            Assert.That(comp.IsDirty(), Is.False);
        }

        [Test]
        public void ClearAllDirtyBitsClearsSyncVarDirtyBits()
        {
            CreateNetworkedAndSpawn(out _, out _, out EmptyBehaviour emptyBehaviour,
                                    out _, out _, out _);

            // set syncinterval so dirtybit works fine
            emptyBehaviour.syncInterval = 0;
            Assert.That(emptyBehaviour.IsDirty(), Is.False);

            // set one syncvar dirty bit
            emptyBehaviour.SetSyncVarDirtyBit(1);
            Assert.That(emptyBehaviour.IsDirty(), Is.True);

            // clear it
            emptyBehaviour.ClearAllDirtyBits();
            Assert.That(emptyBehaviour.IsDirty(), Is.False);
        }

        [Test]
        public void ClearAllDirtyBitsClearsSyncObjectsDirtyBits()
        {
            CreateNetworkedAndSpawn(out _, out _, out NetworkBehaviourWithSyncVarsAndCollections comp,
                                    out _, out _, out _);

            // set syncinterval so dirtybit works fine
            comp.syncInterval = 0;
            Assert.That(comp.IsDirty(), Is.False);

            // dirty the synclist
            comp.list.Add(42);
            Assert.That(comp.IsDirty, Is.True);

            // clear bits should clear synclist bits too
            comp.ClearAllDirtyBits();
            Assert.That(comp.IsDirty, Is.False);
        }

        // NetworkServer.Broadcast clears all dirty bits in all spawned
        // identity's components if they have no observers.
        //
        // this way dirty bit tracking only starts after first observer.
        // otherwise first observer would still get dirty update for everything
        // that was dirty before he observed. even though he already got the
        // full state in spawn packet.
        [Test]
        public void DirtyBitsAreClearedForSpawnedWithoutObservers()
        {
            // need one player, one monster
            CreateNetworkedAndSpawnPlayer(out _, out NetworkIdentity player,
                                          out _, out _,
                                          connectionToClient);
            CreateNetworkedAndSpawn(out _, out NetworkIdentity monster, out NetworkBehaviourWithSyncVarsAndCollections monsterComp,
                                    out _, out _, out _);

            // without AOI, player connection sees everyone automatically.
            // remove the monster from observing.
            // remvoe player from monster observers.
            monster.RemoveObserver(player.connectionToClient);
            Assert.That(monster.observers.Count, Is.EqualTo(0));

            // modify something in the monster so that dirty bit is set
            monsterComp.syncInterval = 0;
            ++monsterComp.health;
            Assert.That(monsterComp.IsDirty(), Is.True);

            // add first observer. dirty bits should be cleared.
            monster.AddObserver(player.connectionToClient);
            Assert.That(monsterComp.IsDirty(), Is.False);
        }
    }

    // hook tests can only be ran when inheriting from NetworkBehaviour
    public class NetworkBehaviourDirtyBitsHookGuardTester : NetworkBehaviour
    {
        [Test]
        public void HookGuard()
        {
            // set hook guard for some bits
            for (int i = 0; i < 10; ++i)
            {
                ulong bit = 1ul << i;

                // should be false by default
                Assert.That(GetSyncVarHookGuard(bit), Is.False);

                // set true
                SetSyncVarHookGuard(bit, true);
                Assert.That(GetSyncVarHookGuard(bit), Is.True);

                // set false again
                SetSyncVarHookGuard(bit, false);
                Assert.That(GetSyncVarHookGuard(bit), Is.False);
            }
        }
    }
}
