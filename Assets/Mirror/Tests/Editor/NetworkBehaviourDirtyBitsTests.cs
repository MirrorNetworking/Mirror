// dirty bits are powerful magic.
// add some tests to guarantee correct behaviour.
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    class NetworkBehaviourWithSyncCollections : NetworkBehaviour
    {
        public SyncList<int> list = new SyncList<int>();
        public SyncDictionary<int, string> dict = new SyncDictionary<int, string>();
    }

    public class NetworkBehaviourDirtyBitsTests : MirrorEditModeTest
    {
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
            Assert.That(comp.DirtyObjectBits(), Is.EqualTo(0b1));

            // set second one dirty. now we should have two dirty bits
            cleanList.Add(43);
            Assert.That(comp.DirtyObjectBits(), Is.EqualTo(0b11));
        }

        [Test]
        public void AnySyncObjectDirty()
        {
            CreateNetworked(out GameObject _, out NetworkIdentity _, out NetworkBehaviourWithSyncCollections comp);

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
