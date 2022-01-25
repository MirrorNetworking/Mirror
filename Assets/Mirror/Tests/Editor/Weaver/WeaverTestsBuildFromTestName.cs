using System.Linq;
using NUnit.Framework;

namespace Mirror.Weaver.Tests
{
    public abstract class WeaverTestsBuildFromTestName : WeaverTests
    {
        [SetUp]
        public virtual void TestSetup()
        {
            string className = TestContext.CurrentContext.Test.ClassName.Split('.').Last();

            BuildAndWeaveTestAssembly(className, TestContext.CurrentContext.Test.Name);
        }

        // IMPORTANT: IsSuccess() tests can almost ALL be moved into regular
        // C#/folders without running AssemblyBuilder on them.
        // See README.md int his folder.
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
