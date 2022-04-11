using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime
{
    public class NetworkIdentityTests : MirrorPlayModeTest
    {
        GameObject gameObject;
        NetworkIdentity identity;

        [UnitySetUp]
        public override IEnumerator UnitySetUp()
        {
            yield return base.UnitySetUp();
            CreateNetworked(out gameObject, out identity);
            yield return null;
        }

        // prevents https://github.com/vis2k/Mirror/issues/1484
        [UnityTest]
        public IEnumerator OnDestroyIsServerTrue()
        {
            // call OnStartServer so that isServer is true
            identity.OnStartServer();
            Assert.That(identity.isServer, Is.True);

            // destroy it
            // note: we need runtime .Destroy instead of edit mode .DestroyImmediate
            //       because we can't check isServer after DestroyImmediate
            GameObject.Destroy(gameObject);

            // make sure that isServer is still true so we can save players etc.
            Assert.That(identity.isServer, Is.True);

            yield return null;
            // Confirm it has been destroyed
            Assert.That(identity == null, Is.True);
        }

        [UnityTest]
        public IEnumerator OnDestroyIsServerTrueWhenNetworkServerDestroyIsCalled()
        {
            // call OnStartServer so that isServer is true
            identity.OnStartServer();
            Assert.That(identity.isServer, Is.True);

            // destroy it
            NetworkServer.Destroy(gameObject);

            // make sure that isServer is still true so we can save players etc.
            Assert.That(identity.isServer, Is.True);

            yield return null;
            // Confirm it has been destroyed
            Assert.That(identity == null, Is.True);
        }

        // imer: There's currently an issue with dropped/skipped serializations
        // once a server has been running for around a week, this test should
        // highlight the potential cause
        [UnityTest]
        public IEnumerator TestSerializationWithLargeTimestamps()
        {
            // 14 * 24 hours per day * 60 minutes per hour * 60 seconds per minute = 14 days
            // NOTE: change this to 'float' to see the tests fail
            int tick = 14 * 24 * 60 * 60;
            NetworkIdentitySerialization serialization = identity.GetSerializationAtTick(tick);
            // advance tick
            ++tick;
            NetworkIdentitySerialization serializationNew = identity.GetSerializationAtTick(tick);

            // if the serialization has been changed the tickTimeStamp should have moved
            Assert.That(serialization.tick == serializationNew.tick, Is.False);
            yield break;
        }
    }
}
