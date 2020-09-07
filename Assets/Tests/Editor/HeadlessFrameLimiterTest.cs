using UnityEngine;
using NUnit.Framework;
using Object = UnityEngine.Object;

namespace Mirror.Tests
{
    [TestFixture]
    public class HeadlessFrameLimiterTest : MonoBehaviour
    {
        protected GameObject testGO;
        protected HeadlessFrameLimiter comp;

        [SetUp]
        public void Setup()
        {
            testGO = new GameObject();
            comp = testGO.AddComponent<HeadlessFrameLimiter>();
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(testGO);
        }

        [Test]
        public void StartOnHeadlessValue()
        {
            Assert.That(comp.serverTickRate, Is.EqualTo(30));
        }
    }
}
