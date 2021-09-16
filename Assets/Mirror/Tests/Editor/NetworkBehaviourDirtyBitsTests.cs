// dirty bits are powerful magic.
// add some tests to guarantee correct behaviour.
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    class NetworkBehaviourWithSyncVarsAndCollections : NetworkBehaviour
    {
        // SyncVars
        [SyncVar] public int health;
        [SyncVar] public int mana;

        // SyncCollections
        public SyncList<int> list = new SyncList<int>();
        public SyncDictionary<int, string> dict = new SyncDictionary<int, string>();
    }

    public class NetworkBehaviourSyncVarDirtyBitsExposed : NetworkBehaviour
    {
        public ulong syncVarDirtyBitsExposed => syncVarDirtyBits;
    }

    public class NetworkBehaviourDirtyBitsTests : MirrorEditModeTest
    {
        [Test]
        public void SetDirtyBit()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity _, out NetworkBehaviourSyncVarDirtyBitsExposed comp);

            // set 3rd dirty bit.
            comp.SetDirtyBit(0b_00000000_00000100);
            Assert.That(comp.syncVarDirtyBitsExposed, Is.EqualTo(0b_00000000_00000100));

            // set 5th dirty bit.
            // both 3rd and 5th should be set.
            comp.SetDirtyBit(0b_00000000_00010000);
            Assert.That(comp.syncVarDirtyBitsExposed, Is.EqualTo(0b_00000000_00010100));
        }

        [Test]
        public void DirtyObjectBits()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity _, out NetworkBehaviourInitSyncObjectExposed comp);

            // not dirty by default
            Assert.That(comp.DirtyObjectBits(), Is.EqualTo(0b0));

            // add a dirty synclist
            SyncList<int> dirtyList = new SyncList<int>();
            dirtyList.Add(42);
            Assert.That(dirtyList.IsDirty, Is.True);
            comp.InitSyncObjectExposed(dirtyList);

            // add a clean synclist
            SyncList<int> cleanList = new SyncList<int>();
            Assert.That(cleanList.IsDirty, Is.False);
            comp.InitSyncObjectExposed(cleanList);

            // get bits - only first one should be dirty
            Assert.That(comp.DirtyObjectBits(), Is.EqualTo(0b01));

            // set second one dirty. now we should have two dirty bits
            cleanList.Add(43);
            Assert.That(comp.DirtyObjectBits(), Is.EqualTo(0b11));
        }

        [Test]
        public void AnySyncObjectDirty()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity _, out NetworkBehaviourWithSyncVarsAndCollections comp);

            // not dirty by default
            Assert.That(comp.AnySyncObjectDirty(), Is.False);

            // change the list. should be dirty now.
            comp.list.Add(42);
            Assert.That(comp.AnySyncObjectDirty(), Is.True);

            // change the dict. should still be dirty.
            comp.dict[42] = null;
            Assert.That(comp.AnySyncObjectDirty(), Is.True);

            // set list not dirty. dict should still make it dirty.
            comp.list.Flush();
            Assert.That(comp.AnySyncObjectDirty(), Is.True);
        }

        [Test]
        public void IsDirty()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity _, out NetworkBehaviourWithSyncVarsAndCollections comp);

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
            CreateNetworked(out GameObject _, out NetworkIdentity _, out EmptyBehaviour emptyBehaviour);

            // set syncinterval so dirtybit works fine
            emptyBehaviour.syncInterval = 0;
            Assert.That(emptyBehaviour.IsDirty(), Is.False);

            // set one syncvar dirty bit
            emptyBehaviour.SetDirtyBit(1);
            Assert.That(emptyBehaviour.IsDirty(), Is.True);

            // clear it
            emptyBehaviour.ClearAllDirtyBits();
            Assert.That(emptyBehaviour.IsDirty(), Is.False);
        }

        [Test]
        public void ClearAllDirtyBitsClearsSyncObjectsDirtyBits()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity _, out NetworkBehaviourInitSyncObjectExposed comp);

            // set syncinterval so dirtybit works fine
            comp.syncInterval = 0;
            Assert.That(comp.IsDirty(), Is.False);

            // create a synclist and dirty it
            SyncList<int> obj = new SyncList<int>();
            obj.Add(42);
            Assert.That(obj.IsDirty, Is.True);

            // add it
            comp.InitSyncObjectExposed(obj);
            Assert.That(comp.IsDirty, Is.True);

            // clear bits should clear synclist bits too
            comp.ClearAllDirtyBits();
            Assert.That(comp.IsDirty, Is.False);
            Assert.That(obj.IsDirty, Is.False);
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
