using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public class WeaverGeneralTests : WeaverTestsBuildFromTestName
    {
        [Test]
        public void RecursionCount()
        {
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.True);
            Assert.That(weaverErrors, Contains.Item("Mirror.Weaver error: MirrorTest.MirrorTestPlayer/Potato1 can't be serialized because it references itself"));
        }
        [Test]
        public void TestingScriptableObjectArraySerialization()
        {
            UnityEngine.Debug.Log(string.Join("\n", weaverErrors));
            Assert.That(CompilationFinishedHook.WeaveFailed, Is.False);
        }
    }
}
