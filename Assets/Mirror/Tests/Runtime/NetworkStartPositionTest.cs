using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mirror.Tests.Runtime
{
    public class NetworkStartPositionTest : MirrorPlayModeTest
    {
        [UnitySetUp]
        public override IEnumerator UnitySetUp()
        {
            NetworkManager.startPositions.Clear();

            base.SetUp();
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartPositionGetsAddedOnAwake()
        {
            Assert.That(NetworkManager.startPositions, Is.Empty);

            CreateGameObject(out GameObject startPositionGameObject, out NetworkStartPosition networkStartPosition);
            yield return null;

            Assert.That(NetworkManager.startPositions, Has.Member(startPositionGameObject.transform));

            Object.Destroy(startPositionGameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StartPositionGetsRemovedOnDestroy()
        {
            Assert.That(NetworkManager.startPositions, Is.Empty);

            CreateGameObject(out GameObject startPositionGameObject, out NetworkStartPosition networkStartPosition);
            yield return null;

            Assert.That(NetworkManager.startPositions, Has.Member(startPositionGameObject.transform));

            Object.Destroy(startPositionGameObject);
            yield return null;

            Assert.That(NetworkManager.startPositions, Is.Empty);
        }
    }
}
