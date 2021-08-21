using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Debug = UnityEngine.Debug;

namespace Mirror.Weaver.Tests
{
    public abstract class WeaverTestsBuildFromTestName : WeaverTests
    {
        [SetUp]
        public virtual void TestSetup()
        {
            string className = TestContext.CurrentContext.Test.ClassName.Split('.').Last();

            Stopwatch watch = new Stopwatch();
            watch.Start();
            BuildAndWeaveTestAssembly(className, TestContext.CurrentContext.Test.Name);
            Debug.LogWarning("BuildAndWeaveTestAssembly: " + watch.ElapsedMilliseconds + "ms");
        }

        protected void IsSuccess()
        {
            Assert.That(weaverErrors, Is.Empty);
            Assert.That(weaverWarnings, Is.Empty);
        }

        protected void HasNoErrors()
        {
            Assert.That(weaverErrors, Is.Empty);
        }

        protected void HasError(string messsage, string atType)
        {
            Assert.That(weaverErrors, Contains.Item($"{messsage} (at {atType})"));
        }

        protected void HasWarning(string messsage, string atType)
        {
            Assert.That(weaverWarnings, Contains.Item($"{messsage} (at {atType})"));
        }
    }
}
