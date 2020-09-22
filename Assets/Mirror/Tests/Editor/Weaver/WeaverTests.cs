using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    [TestFixture]
    [Category("Weaver")]
    public abstract class WeaverTests
    {
        protected List<string> weaverErrors = new List<string>();
        void HandleWeaverError(string msg)
        {
            LogAssert.ignoreFailingMessages = true;
            Debug.LogError(msg);
            LogAssert.ignoreFailingMessages = false;

            weaverErrors.Add(msg);
        }

        protected List<string> weaverWarnings = new List<string>();
        void HandleWeaverWarning(string msg)
        {
            Debug.LogWarning(msg);
            weaverWarnings.Add(msg);
        }

        protected void BuildAndWeaveTestAssembly(string className, string testName)
        {
            string testSourceDirectory = className + "~";
            WeaverAssembler.OutputFile = Path.Combine(testSourceDirectory, testName + ".dll");
            WeaverAssembler.AddSourceFiles(new string[] { Path.Combine(testSourceDirectory, testName + ".cs") });
            WeaverAssembler.Build();

            Assert.That(WeaverAssembler.CompilerErrors, Is.False);
            foreach (string error in weaverErrors)
            {
                // ensure all errors have a location
                Assert.That(error, Does.Match(@"\(at .*\)$"));
            }
        }

        [OneTimeSetUp]
        public void FixtureSetup()
        {
            // TextRenderingModule is only referenced to use TextMesh type to throw errors about types from another module
            WeaverAssembler.AddReferencesByAssemblyName(new string[] { "UnityEngine.dll", "UnityEngine.CoreModule.dll", "UnityEngine.TextRenderingModule.dll", "Mirror.dll" });

            CompilationFinishedHook.UnityLogEnabled = false;
            CompilationFinishedHook.OnWeaverError += HandleWeaverError;
            CompilationFinishedHook.OnWeaverWarning += HandleWeaverWarning;
        }

        [OneTimeTearDown]
        public void FixtureCleanup()
        {
            CompilationFinishedHook.OnWeaverError -= HandleWeaverError;
            CompilationFinishedHook.OnWeaverWarning -= HandleWeaverWarning;
            CompilationFinishedHook.UnityLogEnabled = true;
        }

        [TearDown]
        public void TestCleanup()
        {
            WeaverAssembler.DeleteOutputOnClear = true;
            WeaverAssembler.Clear();

            weaverWarnings.Clear();
            weaverErrors.Clear();
        }
    }
}
