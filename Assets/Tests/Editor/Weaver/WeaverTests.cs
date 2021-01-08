using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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

        protected void IsSuccess()
        {
            Assert.That(weaverLog.errors, Is.Empty);
            Assert.That(weaverLog.warnings, Is.Empty);
        }

        protected void HasNoErrors()
        {
            Assert.That(weaverLog.errors, Is.Empty);
        }

        protected void HasError(string messsage, string atType)
        {
            Assert.That(weaverLog.errors, Contains.Item($"{messsage} (at {atType})"));
        }

        protected void HasWarning(string messsage, string atType)
        {
            Assert.That(weaverLog.warnings, Contains.Item($"{messsage} (at {atType})"));
        }
    }

    [TestFixture]
    [Category("Weaver")]
    public abstract class WeaverTests
    {
        public static readonly ILogger logger = LogFactory.GetLogger<WeaverTests>(LogType.Exception);

        protected class TestLogger : IWeaverLogger
        {
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();

            public void Error(string msg)
            {
                errors.Add(msg);
            }

            public void Error(string message, MemberReference mr)
            {
                Error($"{message} (at {mr})");
            }

            public void Warning(string msg)
            {
                warnings.Add(msg);
            }

            public void Warning(string message, MemberReference mr)
            {
                Warning($"{message} (at {mr})");
            }
        }

        protected TestLogger weaverLog = new TestLogger();

        protected void BuildAndWeaveTestAssembly(string className, string testName)
        {
            string testSourceDirectory = className + "~";
            WeaverAssembler.OutputFile = Path.Combine(testSourceDirectory, testName + ".dll");
            WeaverAssembler.AddSourceFiles(new string[] { Path.Combine(testSourceDirectory, testName + ".cs") });
            WeaverAssembler.Build();

            Assert.That(WeaverAssembler.CompilerErrors, Is.False);
            foreach (string error in weaverLog.errors)
            {
                // ensure all errors have a location
                Assert.That(error, Does.Match(@"\(at .*\)$"));
            }
        }

        [OneTimeSetUp]
        public void FixtureSetup()
        {
            weaverLog = new TestLogger();
            CompilationFinishedHook.logger = weaverLog;
        }

        [OneTimeTearDown]
        public void FixtureCleanup()
        {
            CompilationFinishedHook.logger = null;
        }

        [TearDown]
        public void TestCleanup()
        {
            WeaverAssembler.DeleteOutputOnClear = true;
            WeaverAssembler.Clear();
            weaverLog.errors.Clear();
            weaverLog.warnings.Clear();
        }
    }
}
