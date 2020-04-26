using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverGeneralTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void RecursionCount()
        {
            Assert.That(weaverErrors, Contains.Item("Potato1 can't be serialized because it references itself (at WeaverGeneralTests.RecursionCount.RecursionCount/Potato1)"));
        }

        [Test]
        public void TestingScriptableObjectArraySerialization()
        {
            Assert.That(weaverErrors, Is.Empty);
        }
    }
}
