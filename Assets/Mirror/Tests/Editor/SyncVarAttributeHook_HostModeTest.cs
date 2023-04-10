using NUnit.Framework;

namespace Mirror.Tests.SyncVarAttributeTests
{
    class SyncVarDeepInheritanceHookGuard_0 : NetworkBehaviour
    {
        [SyncVar]
        public int int0;
    }
    class SyncVarDeepInheritanceHookGuard_1 : SyncVarDeepInheritanceHookGuard_0
    {
        [SyncVar(hook = nameof(OnInt1Changed))]
        public int int1;

        private void OnInt1Changed(int oldValue, int newValue)
        {
            int0--;
        }
    }
    class SyncVarDeepInheritanceHookGuard_2 : SyncVarDeepInheritanceHookGuard_1
    {
        [SyncVar(hook = nameof(OnInt2Changed))]
        public int int2;

        private void OnInt2Changed(int oldValue, int newValue)
        {
            int1--;
        }
    }

    public class SyncVarAttributeHook_HostModeTest : MirrorTest
    {
        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            // start server & connect client because we need spawn functions
            NetworkServer.Listen(1);

            // need host mode!
            ConnectHostClientBlockingAuthenticatedAndReady();
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
        }

        // previously there was 0 coverage for [SyncVar] setters properly
        // calling static hook functions.
        // prevents: https://github.com/vis2k/Mirror/pull/3101
        [Test]
        public void StaticMethod_HookCalledFromSyncVarSetter()
        {
            CreateNetworkedAndSpawn(out _, out _, out StaticHookBehaviour comp);

            const int serverValue = 24;

            // hooks setters are only called if activeHost
            Assert.That(NetworkServer.activeHost, Is.True);

            int hookcallCount = 0;
            StaticHookBehaviour.HookCalled += (oldValue, newValue) =>
            {
                hookcallCount++;
                Assert.That(oldValue, Is.EqualTo(0));
                Assert.That(newValue, Is.EqualTo(serverValue));
            };

            // change it on server.
            // the client is active too.
            // so the setter should call the hook.
            comp.value = serverValue;
            Assert.That(hookcallCount, Is.EqualTo(1));
        }

        // previously there was 0 coverage for [SyncVar] setters properly
        // calling virtual / overwritten hook functions.
        // prevents: https://github.com/vis2k/Mirror/pull/3102
        [Test]
        public void VirtualHook_HookCalledWhenSyncingChangedValued()
        {
            CreateNetworkedAndSpawn(out _, out _, out VirtualHookBase comp);

            const int serverValue = 24;

            // hooks setters are only called if activeHost
            Assert.That(NetworkServer.activeHost, Is.True);

            int baseCallCount = 0;
            comp.BaseHookCalled += (oldValue, newValue) =>
            {
                baseCallCount++;
                Assert.That(oldValue, Is.EqualTo(0));
                Assert.That(newValue, Is.EqualTo(serverValue));
            };

            // change it on server.
            // the client is active too.
            // so the setter should call the hook.
            comp.value = serverValue;
            Assert.That(baseCallCount, Is.EqualTo(1));
        }

        // previously there was 0 coverage for [SyncVar] setters properly
        // calling virtual / overwritten hook functions.
        // prevents: https://github.com/vis2k/Mirror/pull/3102
        [Test]
        public void VirtualOverrideHook_HookCalledWhenSyncingChangedValued()
        {
            CreateNetworkedAndSpawn(out _, out _, out VirtualOverrideHook comp);

            const int serverValue = 24;

            // hooks setters are only called if activeHost
            Assert.That(NetworkServer.activeHost, Is.True);

            // hook should change it on client
            int overrideCallCount = 0;
            int baseCallCount = 0;
            comp.OverrideHookCalled += (oldValue, newValue) =>
            {
                overrideCallCount++;
                Assert.That(oldValue, Is.EqualTo(0));
                Assert.That(newValue, Is.EqualTo(serverValue));
            };
            comp.BaseHookCalled += (oldValue, newValue) =>
            {
                baseCallCount++;
            };

            // change it on server
            comp.value = serverValue;

            Assert.That(overrideCallCount, Is.EqualTo(1));
            Assert.That(baseCallCount, Is.EqualTo(0));
        }

        // SyncVar hook guards are in place to prevent infinite hook loop in host mode.
        // Because of #3457 this can throw false positive and block setters and hooks of other SyncVars.
        [Test]
        public void DeepInheritanceSyncVarHookGuardFalsePositive()
        {
            CreateNetworkedAndSpawn(out _, out _, out SyncVarDeepInheritanceHookGuard_2 comp);

            // hooks setters are only called if activeHost
            Assert.That(NetworkServer.activeHost, Is.True);

            comp.int0 = 10;
            comp.int1 = 20; // decrements int0 in hook.
            comp.int2 = 30; // decrements int1 in hook. which then decrements int0.

            // int0 should have been decremented twice.
            Assert.That(comp.int0, Is.EqualTo(10 - 2), "hook guard should not block hook");
            // int1 should have been decremented once.
            Assert.That(comp.int1, Is.EqualTo(20 - 1), "hook guard should not block hook");
        }
    }
}
