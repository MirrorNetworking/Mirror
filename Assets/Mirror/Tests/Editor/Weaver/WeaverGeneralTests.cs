using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverGeneralTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void RecursionCount()
        {
            HasError("Potato1 can't be serialized because it references itself",
                "WeaverGeneralTests.RecursionCount.RecursionCount/Potato1");
        }

        [Test]
        public void TestingScriptableObjectArraySerialization()
        {
            IsSuccess();
        }
    }
}
