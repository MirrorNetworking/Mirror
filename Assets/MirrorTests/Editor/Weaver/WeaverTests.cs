using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Mirror.Weaver.Tests
{
    public abstract class WeaverTestsBuildFromTestName : WeaverTests
    {
        [SetUp]
        public void TestSetup()
        {
            string className = TestContext.CurrentContext.Test.ClassName.Split('.').Last();

            BuildAndWeaveTestAssembly(className, TestContext.CurrentContext.Test.Name);
        }
    }
    [TestFixture]
    [Category("Weaver")]
    public abstract class WeaverTests
    {
        public static readonly ILogger logger = LogFactory.GetLogger<WeaverTests>(LogType.Exception);

        protected List<string> weaverErrors = new List<string>();
        void HandleWeaverError(string msg)
        {
            logger.LogError(msg);
            weaverErrors.Add(msg);
        }

        protected List<string> weaverWarnings = new List<string>();
        void HandleWeaverWarning(string msg)
        {
            logger.LogWarning(msg);
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
            if (weaverErrors.Count > 0)
                Assert.That(CompilationFinishedHook.WeaveFailed, Is.True, "Weaver should fail if there are errors");
            else
                Assert.That(CompilationFinishedHook.WeaveFailed, Is.False, "Weaver should succeed if there are no errors");
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
