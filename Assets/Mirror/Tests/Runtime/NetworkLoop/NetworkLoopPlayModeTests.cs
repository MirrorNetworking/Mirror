// Play mode tests for NetworkLoop.NetworkEarlyUpdate and NetworkLateUpdate.
// These methods guard on Application.isPlaying, so their bodies can only be
// reached (and covered) when the test runner is in play mode.
//
// NetworkServer/NetworkClient are safe to call here when not started:
//   - NetworkServer checks 'if (active)' before any meaningful work
//   - NetworkClient checks 'if (active)' and 'if (Transport.active != null)'
// No transport or server/client setup is required.
using System.Reflection;
using NUnit.Framework;

namespace Mirror.Tests
{
    public class NetworkLoopPlayModeTests
    {
        [SetUp]
        public void SetUp()
        {
            NetworkLoop.OnEarlyUpdate = null;
            NetworkLoop.OnLateUpdate  = null;
        }

        [TearDown]
        public void TearDown()
        {
            NetworkLoop.OnEarlyUpdate = null;
            NetworkLoop.OnLateUpdate  = null;
        }

        // invoke a private static method on NetworkLoop
        static void Invoke(string methodName) =>
            typeof(NetworkLoop)
                .GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, null);

        // =====================================================================
        // NetworkEarlyUpdate
        // =====================================================================

        // OnEarlyUpdate is null → the ?. operator skips the invoke (no throw).
        // covers the null branch of OnEarlyUpdate?.Invoke()
        [Test]
        public void NetworkEarlyUpdate_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => Invoke("NetworkEarlyUpdate"));
        }

        // OnEarlyUpdate has a subscriber → must be invoked after
        // NetworkServer/Client have updated.
        // covers the non-null branch of OnEarlyUpdate?.Invoke()
        [Test]
        public void NetworkEarlyUpdate_InvokesOnEarlyUpdate()
        {
            bool called = false;
            NetworkLoop.OnEarlyUpdate += () => called = true;

            Invoke("NetworkEarlyUpdate");

            Assert.That(called, Is.True);
        }

        // =====================================================================
        // NetworkLateUpdate
        // =====================================================================

        // OnLateUpdate is null → the ?. operator skips the invoke (no throw).
        // covers the null branch of OnLateUpdate?.Invoke()
        [Test]
        public void NetworkLateUpdate_NullCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => Invoke("NetworkLateUpdate"));
        }

        // OnLateUpdate has a subscriber → must be invoked before
        // NetworkServer/Client have updated.
        // covers the non-null branch of OnLateUpdate?.Invoke()
        [Test]
        public void NetworkLateUpdate_InvokesOnLateUpdate()
        {
            bool called = false;
            NetworkLoop.OnLateUpdate += () => called = true;

            Invoke("NetworkLateUpdate");

            Assert.That(called, Is.True);
        }
    }
}