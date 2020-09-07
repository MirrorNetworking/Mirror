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

            var gameObject = new GameObject();
            networkTransformChild = gameObject.AddComponent<NetworkTransformChild>();

            Assert.That(networkTransformChild.Target == null);

            networkTransformChild.Target = gameObject.transform;

            Assert.That(networkTransformChild.Target == networkTransformChild.transform);
        }
    }
}
