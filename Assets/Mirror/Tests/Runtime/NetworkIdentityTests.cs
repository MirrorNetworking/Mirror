using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime
{
    public class NetworkIdentityTests
    {
        GameObject gameObject;
        NetworkIdentity identity;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject();
            identity = gameObject.AddComponent<NetworkIdentity>();
        }

        [TearDown]
        public void TearDown()
        {
            GameObject.Destroy(gameObject);
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
    }
}
