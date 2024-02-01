// default = no component = everyone sees everyone
using NUnit.Framework;

namespace Mirror.Tests.InterestManagement
{
    public class InterestManagementTests_Default : InterestManagementTests_Common
    {
        // no interest management (default)
        // => forceHidden should still work
        [Test]
        public override void ForceHidden_Initial()
        {
            // force hide A
            identityA.visibility = Visibility.ForceHidden;

            // rebuild for both
            // initial rebuild adds all connections if no interest management available
            NetworkServer.RebuildObservers(identityA, true);
            NetworkServer.RebuildObservers(identityB, true);

            // A should not be seen by B because A is force hidden
            Assert.That(identityA.observers.ContainsKey(connectionB.connectionId), Is.False);
            // B should be seen by A because
            Assert.That(identityB.observers.ContainsKey(connectionA.connectionId), Is.True);
        }

        // no interest management (default)
        // => forceShown should still work
        [Test]
        public override void ForceShown_Initial()
        {
            // force show A
            identityA.visibility = Visibility.ForceShown;

            // rebuild for both
            // initial rebuild adds all connections if no interest management available
            NetworkServer.RebuildObservers(identityA, true);
            NetworkServer.RebuildObservers(identityB, true);

            // both should see each other because by default, everyone sees everyone
            Assert.That(identityA.observers.ContainsKey(connectionB.connectionId), Is.True);
            Assert.That(identityB.observers.ContainsKey(connectionA.connectionId), Is.True);
        }

        // no interest management (default)
        // => everyone should see everyone
        [Test]
        public void EveryoneSeesEveryone_Initial()
        {
            // rebuild for both
            // initial rebuild adds all connections if no interest management available
            NetworkServer.RebuildObservers(identityA, true);
            NetworkServer.RebuildObservers(identityB, true);

            // both should see each other
            Assert.That(identityA.observers.ContainsKey(connectionB.connectionId), Is.True);
            Assert.That(identityB.observers.ContainsKey(connectionA.connectionId), Is.True);
        }

        // TODO add tests to make sure old observers are removed etc.
    }
}
