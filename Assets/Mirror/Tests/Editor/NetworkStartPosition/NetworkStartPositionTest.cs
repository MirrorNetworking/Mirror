using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    public class NetworkStartPositionTest : MirrorEditModeTest
    {
        [SetUp]
        public override void SetUp()
        {
            NetworkManager.startPositions.Clear();
            base.SetUp();
        }

        [TearDown]
        public override void TearDown()
        {
            NetworkManager.startPositions.Clear();
            base.TearDown();
        }

        [Test]
        public void NetworkStartPositionLifecycleTest()
        {
            Assert.That(NetworkManager.startPositions.Count, Is.Zero);

            CreateGameObject(out GameObject startPositionGameObject, out NetworkStartPosition networkStartPosition);

            // Must call Unity lifecycle methods manually in edit mode tests
            networkStartPosition.Awake();
            Assert.That(NetworkManager.startPositions.Count, Is.EqualTo(1));
            Assert.That(NetworkManager.startPositions, Has.Member(startPositionGameObject.transform));

            // Must call Unity lifecycle methods manually in edit mode tests
            networkStartPosition.OnDestroy();
            Object.DestroyImmediate(startPositionGameObject);

            Assert.That(NetworkManager.startPositions.Count, Is.Zero);
        }
    }
}
