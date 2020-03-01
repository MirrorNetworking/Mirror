using NUnit.Framework;
using UnityEngine;

namespace Mirror.Tests
{
    [TestFixture]
    public class NetworkTransformChildTest
    {
        NetworkTransformChild  networkTransformChild;

        [Test]
        public void TargetComponentTest()
        {
            GameObject gameObject = new GameObject();
            networkTransformChild = gameObject.AddComponent<NetworkTransformChild>();

            Assert.That(networkTransformChild.target == null);

            networkTransformChild.target = gameObject.transform;

            Assert.That(networkTransformChild.target == networkTransformChild.transform);
        }
    }
}
