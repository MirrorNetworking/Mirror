using NUnit.Framework;

namespace Mirror.Tests.SyncVarAttributeTests
{
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

            // hooks setters are only called if localClientActive
            Assert.That(NetworkServer.localClientActive, Is.True);

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

            // hooks setters are only called if localClientActive
            Assert.That(NetworkServer.localClientActive, Is.True);

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

            // hooks setters are only called if localClientActive
            Assert.That(NetworkServer.localClientActive, Is.True);

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
    }
}
