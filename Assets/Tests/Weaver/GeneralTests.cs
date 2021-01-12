using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class GeneralTests : TestsBuildFromTestName
    {
        [Test]
        public void RecursionCount()
        {
            IsSuccess();
        }

        [Test]
        public void TestingScriptableObjectArraySerialization()
        {
            IsSuccess();
        }
    }
}
