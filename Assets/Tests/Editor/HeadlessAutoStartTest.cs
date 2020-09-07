using UnityEngine;
using NUnit.Framework;
using Object = UnityEngine.Object;

namespace Mirror.Tests
{
    [TestFixture]
    public class HeadlessAutoStartTest : MonoBehaviour
    {
        protected GameObject testGO;
        protected HeadlessAutoStart comp;

        [SetUp]
        public void Setup()
        {
            testGO = new GameObject();
            comp = testGO.AddComponent<HeadlessAutoStart>();
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(testGO);
        }

        [Test]
        public void StartOnHeadlessValue()
        {
            Assert.That(comp.startOnHeadless, Is.True);
        }
    }
}
