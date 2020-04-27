using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    [TestFixture]
    public class NetworkTransformChildTest
    {
        [Test]
        public void TargetComponentTest()
        {
            NetworkTransformChild networkTransformChild;

            GameObject gameObject = new GameObject();
            networkTransformChild = gameObject.AddComponent<NetworkTransformChild>();

            Assert.That(networkTransformChild.target == null);

            networkTransformChild.target = gameObject.transform;

            Assert.That(networkTransformChild.target == networkTransformChild.transform);
        }
    }
}
