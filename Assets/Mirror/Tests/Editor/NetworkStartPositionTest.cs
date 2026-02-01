using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests.Editor
{
    public class NetworkStartPositionTest : MirrorEditModeTest
    {
        [SetUp]
        public override void SetUp()
        {
            NetworkManager.startPositions.Clear();

            base.SetUp();
        }

        [Test]
        public void NetworkStartPositionOnAwakeTest()
        {
            Assert.That(NetworkManager.startPositions.Count, Is.Zero);

            CreateGameObject(out GameObject startPositionGameObject, out NetworkStartPosition networkStartPosition);

            networkStartPosition.Awake();
            Assert.That(NetworkManager.startPositions.Count, Is.EqualTo(1));
            Assert.That(NetworkManager.startPositions, Has.Member(startPositionGameObject.transform));

            Object.DestroyImmediate(startPositionGameObject);
            NetworkManager.startPositions.Clear();
        }

        [Test]
        public void NetworkStartPositionOnDestroyTest()
        {
            Assert.That(NetworkManager.startPositions.Count, Is.Zero);

            CreateGameObject(out GameObject startPositionGameObject, out NetworkStartPosition networkStartPosition);

            networkStartPosition.Awake();
            Assert.That(NetworkManager.startPositions.Count, Is.EqualTo(1));
            Assert.That(NetworkManager.startPositions, Has.Member(startPositionGameObject.transform));

            networkStartPosition.OnDestroy();

            Assert.That(NetworkManager.startPositions.Count, Is.Zero);

            Object.DestroyImmediate(startPositionGameObject);
            NetworkManager.startPositions.Clear();
        }
    }
}
